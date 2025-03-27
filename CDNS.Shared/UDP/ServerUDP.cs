using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CDNS.Shared.Models;

namespace CDNS.Shared.UDP;

public class ServerUDP : BaseUDP
{
    // The BaseUDP class is responsible for making sure that the server IP and port are valid.
    public ServerUDP(IPAddress serverIP, int serverPort, string? dnsRecordsPath = null) : base(RoleType.Server, serverIP, serverPort)
        => DnsRecordsPath = dnsRecordsPath;

    public ServerUDP(string? configPath = null, string? dnsRecordsPath = null) : base(RoleType.Server, configPath)
        => DnsRecordsPath = dnsRecordsPath;

    private string _dnsRecordsPath = "dnsRecords.json"; // default path for dns records
    public string? DnsRecordsPath { get { return _dnsRecordsPath; } private set { _dnsRecordsPath = value ?? _dnsRecordsPath; } }
    public List<DNSRecord> DnsRecords { get; private set; } = new List<DNSRecord>();

    // A dictionary to keep track of active connections and their last activity time
    public Dictionary<EndPoint, DateTime> ActiveConnections { get; private set; } = new Dictionary<EndPoint, DateTime>();

    // An arbitrary message ID to keep track of messages
    private int _lastMessageId = 0;

    // A dictionary to keep track of messages that are awaiting acknowledgment
    public Dictionary<int, (Message Message, DateTime LastAttempt, int DeliveryAttempt, EndPoint Reciever)> AwaitingAckMessages { get; private set; } = new Dictionary<int, (Message Message, DateTime LastAttempt, int DeliveryAttempt, EndPoint Reciever)>();

    public void Start()
    {
        // Before startup validate the server IP and port and make sure the sockets are available
        // If not throw an exception
        if (ServerIP is null || ServerPort < 9000)
            throw new InvalidOperationException("Server IP and port must be provided, server port must be 9000 or above.");

        // Load the DNS records from the JSON file
        LoadDnsRecords();

        // Create a socket and endpoints and bind it to the server IP address and port number
        IPEndPoint ipEndPoint = new IPEndPoint(ServerIP, ServerPort);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(ipEndPoint);
        Console.WriteLine($"Server started and listening on {ServerIP}:{ServerPort}...");

        // Buffer for receiving data  
        byte[] buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (true) // Continuously listen for incoming messages  
        {
            try
            {
                // Receive data from a client  
                int receivedBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Console.WriteLine($"Received message from {remoteEndPoint}: {receivedMessage}");

                // Handle the received message (e.g., respond with "Hello", process DNS lookup, etc.)  
                ProcessMessage(receivedMessage, remoteEndPoint, socket);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            if (AwaitingAckMessages.Count > 0)
            {
                // Check if any messages are awaiting acknowledgment
                foreach (var delivery in AwaitingAckMessages.Values)
                {
                    // If the message has not been acknowledged for more than 5 seconds, resend it
                    if (DateTime.Now - delivery.LastAttempt > TimeSpan.FromSeconds(1))
                    {
                        if (delivery.DeliveryAttempt >= 3)
                        {
                            Console.WriteLine($"Resending message ID {delivery.Message.MsgId} to {delivery.Reciever} failed after 3 attempts. Removing from awaiting acknowledgment.");
                            AwaitingAckMessages.Remove(delivery.Message.MsgId);
                            continue;
                        }

                        Console.WriteLine($"Resending message ID {delivery.Message.MsgId} to {delivery.Reciever}...");
                        SendMessage(delivery.Message, delivery.Reciever, socket, delivery.DeliveryAttempt);
                    }
                }
            }

            // Check for inactive connections and remove them from the active connections dictionary
            var inactiveConnections = ActiveConnections.Where(c => DateTime.Now - c.Value > TimeSpan.FromSeconds(60)).ToList();
            foreach (var inactiveConnection in inactiveConnections)
            {
                Console.WriteLine($"Connection {inactiveConnection.Key} has been inactive for more than 60 seconds. Removing from active connections.");
                SendEnd(inactiveConnection.Key, socket);
            }
        }
    }

    private void ProcessMessage(string receivedMessage, EndPoint remoteEndPoint, Socket socket)
    {
        // Deserialize the received message into a Message object
        var message = JsonSerializer.Deserialize<Message>(receivedMessage);

        // If the message is null, the data is malformed
        if (message is null)
        {
            Console.WriteLine("Received malformed message. Sending an error message back to the client.");
            SendMessage("Malformed message received.", MessageType.Error, remoteEndPoint, socket);
            return;
        }

        // Handle the message based on its type
        switch (message.MsgType)
        {
            case MessageType.Hello:
                Console.WriteLine($"Received Hello message from {remoteEndPoint}. Sending Welcome message back.");
                HandleHello(remoteEndPoint, socket);
                break;
            case MessageType.DNSLookup:
                Console.WriteLine($"Received DNSLookup message from {remoteEndPoint}. Processing DNS lookup...");
                ProcessDNSLookup(message, remoteEndPoint, socket);
                break;
            case MessageType.Ack:
                Console.WriteLine($"Received Acknowledgment message from {remoteEndPoint}. Continuing...");
                HandleAck(message, remoteEndPoint, socket);
                break;
            case MessageType.End:
                Console.WriteLine($"Received End message from {remoteEndPoint}. Closing the connection.");
                HandleEnd(remoteEndPoint, socket);
                break;
            default:
                Console.WriteLine($"Received unknown message type from {remoteEndPoint}. Sending an error message back to the client.");
                SendMessage("Unknown message type received.", MessageType.Error, remoteEndPoint, socket);
                break;
        }
    }

    private void HandleHello(EndPoint remoteEndPoint, Socket socket)
    {
        // Track remoteEndPoint for policy enforcement
        ActiveConnections[remoteEndPoint] = DateTime.Now;
        SendWelcome(remoteEndPoint, socket);
    }

    private void HandleEnd(EndPoint remoteEndPoint, Socket socket)
    {
        // Remove remoteEndPoint for policy enforcement
        ActiveConnections.Remove(remoteEndPoint);
        SendEnd(remoteEndPoint, socket);
    }

    private void HandleAck(Message message, EndPoint remoteEndPoint, Socket socket)
    {
        // If the message ID is not found in the awaiting acknowledgment dictionary, the data is malformed
        if (!AwaitingAckMessages.ContainsKey(message.MsgId))
        {
            Console.WriteLine("Received malformed Acknowledgment message. Message ID not found in awaiting acknowledgment dictionary.");
            SendError("Message ID not found in awaiting acknowledgment dictionary.", remoteEndPoint, socket);
            return;
        }

        // Remove the message from the awaiting acknowledgment dictionary
        AwaitingAckMessages.Remove(message.MsgId);
        Console.WriteLine($"Acknowledgment received for message ID {message.MsgId} from {remoteEndPoint}. Continuing...");
    }

    private void ProcessDNSLookup(Message message, EndPoint remoteEndPoint, Socket socket)
    {
        // If the remoteEndPoint is not found in the active connections dictionary, the client has not sent a Hello message
        if (!ActiveConnections.ContainsKey(remoteEndPoint))
        {
            Console.WriteLine($"DNSLookup message received from {remoteEndPoint} before a Hello message. Sending an error message back to the client.");
            SendError("DNSLookup message received before a Hello message.", remoteEndPoint, socket);
            return;
        }

        // Track remoteEndPoint for policy enforcement
        ActiveConnections[remoteEndPoint] = DateTime.Now;

        // If the content of the message is null, the data is malformed
        if (message.Content == null)
        {
            Console.WriteLine("Received malformed DNSLookup message. Sending an error message back to the client. Message content is null.");
            SendError("Domain not found", remoteEndPoint, socket);
            return;
        }

        // Deserialize the content of the message into a string (the domain name to look up)
        var domainName = JsonSerializer.Deserialize<string>(message.Content.ToString()!);

        // If the domain name is null or empty, the data is malformed
        if (string.IsNullOrWhiteSpace(domainName))
        {
            Console.WriteLine("Received malformed DNSLookup message. Sending an error message back to the client. Domain name is null or whitespace.");
            SendError("Domain not found", remoteEndPoint, socket);
            return;
        }

        // Find the DNS record for the domain name
        var dnsRecord = DnsRecords.FirstOrDefault(r => r.Name == domainName);

        // If the record is found, send a DNSLookupReply message with the record
        if (dnsRecord != null)
        {
            Console.WriteLine($"DNS record found for {domainName}. Sending DNSLookupReply message back to {remoteEndPoint}.");
            SendDNSLookupReply(dnsRecord, remoteEndPoint, socket);
        }
        else
        {
            Console.WriteLine($"DNS record not found for {domainName}. Sending an error message back to {remoteEndPoint}.");
            SendError("DNS record not found.", remoteEndPoint, socket);
        }
    }

    private void SendMessage(Message message, EndPoint remoteEndPoint, Socket socket, int deliveryAttempt = 0)
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
            Console.WriteLine($"Error sending message to {remoteEndPoint}: {ex.Message}");
        }

        // If the message type is a DNSLookupReply, add it to the awaiting acknowledgment dictionary
        if (message.MsgType == MessageType.DNSLookupReply)
            AwaitingAckMessages[message.MsgId] = (message, DateTime.Now, ++deliveryAttempt, remoteEndPoint);
    }

    private void SendMessage(string content, MessageType messageType, EndPoint remoteEndPoint, Socket socket)
    {
        // Create a new message with the provided content and type
        var message = new Message
        {
            MsgId = GetNextMessageId(),
            MsgType = messageType,
            Content = content
        };

        SendMessage(message, remoteEndPoint, socket);
    }

    private void SendEnd(EndPoint remoteEndPoint, Socket socket) 
        => SendMessage("End of DNSLookup", MessageType.End, remoteEndPoint, socket);

    private void SendError(string message, EndPoint remoteEndPoint, Socket socket)
        => SendMessage(message, MessageType.Error, remoteEndPoint, socket);

    private void SendDNSLookupReply(DNSRecord dnsRecord, EndPoint remoteEndPoint, Socket socket)
        => SendMessage(JsonSerializer.Serialize(dnsRecord), MessageType.DNSLookupReply, remoteEndPoint, socket);

    private void SendWelcome(EndPoint remoteEndPoint, Socket socket)
        => SendMessage("Welcome from server", MessageType.Welcome, remoteEndPoint, socket);

    /// <summary>
    /// Get the next safe message ID that is not currently in use or awaiting acknowledgment.
    /// </summary>
    private int GetNextMessageId()
    {
        if (_lastMessageId == int.MaxValue)
            _lastMessageId = 0;

        while (AwaitingAckMessages.ContainsKey(++_lastMessageId))
            _lastMessageId++;

        return ++_lastMessageId;
    }

    private void LoadDnsRecords()
    {
        // If the DNS records path is not provided, use the default path.
        // This should not happen unless intentionally messed with during object creation.
        // We are tolerant though and will just use the default path.
        if (string.IsNullOrWhiteSpace(DnsRecordsPath))
        {
            Console.WriteLine("No DNS records path provided. Using default path: dnsRecords.json.");
            DnsRecordsPath = "dnsRecords.json";
        }

        // If the file does not exist, create a new file and write an empty JSON array to it.
        if (!File.Exists(DnsRecordsPath))
        {
            Console.WriteLine($"DNS records file not found at {DnsRecordsPath}. Creating a new file for storage.");
            File.WriteAllText(DnsRecordsPath, JsonSerializer.Serialize(new List<DNSRecord>()));
        }

        // Read the DNS records from the JSON file
        var dnsFileData = File.ReadAllText(DnsRecordsPath);
        var dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsFileData);

        // If the file is not empty but the records are null, the data is malformed.
        if (dnsRecords is null && !string.IsNullOrWhiteSpace(dnsFileData))
            throw new InvalidOperationException($"Failed to read DNS records from {DnsRecordsPath}, and file was not empty! Data must be malformed!");

        // If the records are null, set the records to an empty list.
        DnsRecords = dnsRecords ?? new List<DNSRecord>();

        // Log the number of records loaded from the file.
        Console.WriteLine($"Loaded {DnsRecords.Count} DNS records from {DnsRecordsPath}.");
    }
}