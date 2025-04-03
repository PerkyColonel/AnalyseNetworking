using CDNS.Shared;
using CDNS.Shared.Models;
using CDNS.Shared.UDP;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CDNS.Server.UDP;

public class ServerUDP : BaseUDP
{
    // The BaseUDP class is responsible for making sure that the server IP and port are valid.
    public ServerUDP(IPAddress serverIP, int serverPort, string? dnsRecordsPath = null) : base(RoleType.Server, serverIP, serverPort)
        => DnsRecordsPath = dnsRecordsPath;

    public ServerUDP(string? configPath = null, string? dnsRecordsPath = null) : base(RoleType.Server, configPath)
        => DnsRecordsPath = dnsRecordsPath;

    private string _dnsRecordsPath = "Configurations/dnsRecords.json"; // default path for dns records
    public string? DnsRecordsPath { get { return _dnsRecordsPath; } private set { _dnsRecordsPath = value ?? _dnsRecordsPath; } }
    public List<DnsRecord> DnsRecords { get; private set; } = [];

    // A dictionary to keep track of active connections and their last activity time
    public Dictionary<EndPoint, DateTime> ActiveConnections { get; private set; } = [];

    // A dictionary to keep track of messages that are awaiting acknowledgment
    public Dictionary<(int MsgId, EndPoint Reciever), (Message Message, DateTime LastAttempt, int DeliveryAttempt, EndPoint Reciever)> AwaitingAckMessages { get; private set; } = [];

    public void Start()
    {
        // Before startup validate the server IP and port and make sure the sockets are available
        // If not throw an exception
        if (ServerIP is null || ServerPort < 5000)
            throw new InvalidOperationException("Server IP and port must be provided, server port must be 5000 or above.");

        // Load the DNS records from the JSON file
        LoadDnsRecords();

        // Create a socket and endpoints and bind it to the server IP address and port number
        IPEndPoint ipEndPoint = new IPEndPoint(ServerIP, ServerPort);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Bind the socket to the local endpoint, on failure log the error
        try
        {
            socket.Bind(ipEndPoint);
        }
        catch (SocketException ex)
        {
            Log(LogLevel.Error, $"Failed to bind socket: {ex.Message}");
            Environment.Exit(1);
        }

        Log(LogLevel.Information, $"Server started and listening on {ServerIP}:{ServerPort}.");

        // Buffer for receiving data  
        byte[] buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (true) // Continuously listen for incoming messages  
        {
            ReceiveMessage(socket, ref buffer, ref remoteEndPoint);

            HandleResend(socket);

            HandleInactiveConnections(socket);
        }
    }

    private void HandleInactiveConnections(Socket socket)
    {
        // Check for inactive connections and remove them from the active connections dictionary
        var inactiveConnections = ActiveConnections.Where(c => DateTime.Now - c.Value > TimeSpan.FromSeconds(60)).ToList();
        foreach (var inactiveConnection in inactiveConnections)
        {
            Log(LogLevel.Information, $"Connection {inactiveConnection.Key} has been inactive for more than 60 seconds. Removing from active connections.", remoteEndPoint: inactiveConnection.Key);
            SendEnd(0, inactiveConnection.Key, socket);
        }
    }

    private void HandleResend(Socket socket)
    {
        if (AwaitingAckMessages.Count == 0)
            return;

        // Check if any messages are awaiting acknowledgment
        foreach (var delivery in AwaitingAckMessages.Values)
        {
            // If the message has been resent 3 times, remove it from the awaiting acknowledgment dictionary
            if (delivery.DeliveryAttempt >= 3)
            {
                Log(LogLevel.Debug, $"Resending message failed after 3 attempts.", delivery.Message.MsgType, delivery.Message.MsgId, delivery.Reciever);
                AwaitingAckMessages.Remove((delivery.Message.MsgId, delivery.Reciever));
                continue;
            }

            // If the message has not been acknowledged for more than 5 seconds, resend it
            if (DateTime.Now - delivery.LastAttempt > TimeSpan.FromSeconds(5))
            {
                Log(LogLevel.Debug, $"Resending message...", delivery.Message.MsgType, delivery.Message.MsgId, delivery.Reciever);
                SendMessage(delivery.Message, delivery.Reciever, socket, delivery.DeliveryAttempt);
            }
        }
    }

    private void ReceiveMessage(Socket socket, ref byte[] buffer, ref EndPoint remoteEndPoint)
    {
        try
        {
            // Receive data from a client  
            int receivedBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);

            // Handle the received message (e.g., respond with "Hello", process DNS lookup, etc.)  
            ProcessMessage(receivedMessage, remoteEndPoint, socket);
        }
        catch (SocketException ex)
        {
            Log(LogLevel.Error, $"A socket error occurred: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"An error occurred: {ex.Message}");
        }
    }

    private void ProcessMessage(string receivedMessage, EndPoint remoteEndPoint, Socket socket)
    {
        // Deserialize the received message into a Message object
        var message = JsonSerializer.Deserialize<Message>(receivedMessage);

        // If the message is null, the data is malformed
        if (message is null)
        {
            SendError(-1, "Malformed message received.", remoteEndPoint, socket);
            return;
        }

        Log(LogLevel.Information, message.Content!.ToString()!, message.MsgType, message.MsgId, remoteEndPoint, direction: DirectionType.In);

        // Handle the message based on its type
        switch (message.MsgType)
        {
            case MessageType.Hello:
                ActiveConnections[remoteEndPoint] = DateTime.Now;
                SendWelcome(message.MsgId, remoteEndPoint, socket);
                break;
            case MessageType.DNSLookup:
                ProcessDNSLookup(message.MsgId, message, remoteEndPoint, socket);
                break;
            case MessageType.Ack:
                HandleAck(message.MsgId, message, remoteEndPoint, socket);
                break;
            case MessageType.End:
                ActiveConnections.Remove(remoteEndPoint);
                SendEnd(message.MsgId, remoteEndPoint, socket);
                break;
            default:
                SendError(message.MsgId, "Unknown message type received.", remoteEndPoint, socket);
                break;
        }
    }

    private void HandleAck(int messageId, Message message, EndPoint remoteEndPoint, Socket socket)
    {
        // If the message ID is not found in the awaiting acknowledgment dictionary, the data is malformed
        if (!AwaitingAckMessages.ContainsKey((message.MsgId, remoteEndPoint)))
        {
            SendError(messageId, $"Message ID {messageId} not found.", remoteEndPoint, socket);
            return;
        }

        // Remove the message from the awaiting acknowledgment dictionary
        AwaitingAckMessages.Remove((message.MsgId, remoteEndPoint));
    }

    private void ProcessDNSLookup(int messageId, Message message, EndPoint remoteEndPoint, Socket socket)
    {
        // If the remoteEndPoint is not found in the active connections dictionary, the client has not sent a Hello message
        if (!ActiveConnections.ContainsKey(remoteEndPoint))
        {
            SendError(messageId, "DNSLookup message received before a Hello message.", remoteEndPoint, socket);
            return;
        }

        // Track remoteEndPoint for policy enforcement
        ActiveConnections[remoteEndPoint] = DateTime.Now;

        // If the content of the message is null, the data is malformed
        if (message.Content == null)
        {
            SendError(messageId, "Domain not found", remoteEndPoint, socket, true);
            return;
        }

        // Convert the content of the message to a string
        var lookupRequest = message.Content.ToString();
        var dnsLookup = JsonSerializer.Deserialize<DnsLookup>(lookupRequest);

        // If the lookup request is null, the data is malformed
        if (dnsLookup == null)
        {
            SendError(messageId, "Domain not found.", remoteEndPoint, socket, true);
            return;
        }

        // If the domain name is null or empty, the data is malformed
        if (string.IsNullOrWhiteSpace(lookupRequest))
        {
            SendError(messageId, "Domain not found", remoteEndPoint, socket, true);
            return;
        }

        // Find the DNS record for the domain name
        var dnsRecord = DnsRecords.FirstOrDefault(r => r.Name == dnsLookup.Name && r.Type == dnsLookup.Type.ToString());

        // If the record is found, send a DNSLookupReply message with the record
        if (dnsRecord != null)
            SendDNSLookupReply(messageId, dnsRecord, remoteEndPoint, socket);
        else
            SendError(messageId, "DNS record not found.", remoteEndPoint, socket, true);
    }

    private void SendMessage(Message message, EndPoint remoteEndPoint, Socket socket, int deliveryAttempt = 0, bool ackExpected = false)
    {
        // Serialize the message into a JSON string
        var messageJson = JsonSerializer.Serialize(message);

        // Convert the message into a byte array
        byte[] buffer = Encoding.UTF8.GetBytes(messageJson);

        // Send the message to the remote endpoint
        try
        {
            socket.SendTo(buffer, remoteEndPoint);
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Error sending message: {ex.Message}", message.MsgType, message.MsgId, remoteEndPoint);
        }

        // If the message type is a DNSLookupReply, add it to the awaiting acknowledgment dictionary
        if (message.MsgType == MessageType.DNSLookupReply || ackExpected)
            AwaitingAckMessages[(message.MsgId, remoteEndPoint)] = (message, DateTime.Now, ++deliveryAttempt, remoteEndPoint);
    }

    private void SendMessage(int messageId, string content, MessageType messageType, EndPoint remoteEndPoint, Socket socket, bool ackExpected = false)
    {
        // Create a new message with the provided content and type
        var message = new Message
        {
            MsgId = messageId,
            MsgType = messageType,
            Content = content
        };

        Log(messageType == MessageType.Error ? LogLevel.Error : LogLevel.Information, content, messageType, messageId, remoteEndPoint, DirectionType.Out);

        SendMessage(message, remoteEndPoint, socket, ackExpected: ackExpected);
    }

    private void SendEnd(int messageId, EndPoint remoteEndPoint, Socket socket)
        => SendMessage(messageId, "End of DNSLookup", MessageType.End, remoteEndPoint, socket);

    private void SendError(int messageId, string message, EndPoint remoteEndPoint, Socket socket, bool ackExpected = false)
        => SendMessage(messageId, message, MessageType.Error, remoteEndPoint, socket, ackExpected);

    private void SendDNSLookupReply(int messageId, DnsRecord dnsRecord, EndPoint remoteEndPoint, Socket socket)
        => SendMessage(messageId, JsonSerializer.Serialize(dnsRecord), MessageType.DNSLookupReply, remoteEndPoint, socket);

    private void SendWelcome(int messageId, EndPoint remoteEndPoint, Socket socket)
        => SendMessage(messageId, "Welcome from server", MessageType.Welcome, remoteEndPoint, socket);

    private void LoadDnsRecords()
    {
        // If the DNS records path is not provided, use the default path.
        // This should not happen unless intentionally messed with during object creation.
        // We are tolerant though and will just use the default path.
        if (string.IsNullOrWhiteSpace(DnsRecordsPath))
        {
            Log(LogLevel.Information, "No DNS records path provided. Using default path: dnsRecords.json.");
            DnsRecordsPath = "Configurations/dnsRecords.json";
        }

        // If the file does not exist, create a new file and write an empty JSON array to it.
        if (!File.Exists(DnsRecordsPath))
        {
            Log(LogLevel.Information, $"DNS records file not found at {DnsRecordsPath}. Creating a new file for storage.");
            File.WriteAllText(DnsRecordsPath, JsonSerializer.Serialize(new List<DnsRecord>()));
        }

        // Read the DNS records from the JSON file
        var dnsFileData = File.ReadAllText(DnsRecordsPath);
        var dnsRecords = JsonSerializer.Deserialize<List<DnsRecord>>(dnsFileData);

        // If the file is not empty but the records are null, the data is malformed.
        if (dnsRecords is null && !string.IsNullOrWhiteSpace(dnsFileData))
            throw new InvalidOperationException($"Failed to read DNS records from {DnsRecordsPath}, and file was not empty! Data must be malformed!");

        // If the records are null, set the records to an empty list.
        DnsRecords = dnsRecords ?? new List<DnsRecord>();

        // Log the number of records loaded from the file.
        Log(LogLevel.Information, $"Loaded {DnsRecords.Count} DNS records from {DnsRecordsPath}.");
    }
}