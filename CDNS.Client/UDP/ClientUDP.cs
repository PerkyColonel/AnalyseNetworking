using CDNS.Shared;
using CDNS.Shared.Models;
using CDNS.Shared.UDP;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CDNS.Client.UDP;

public class ClientUDP : BaseUDP
{
    // The BaseUDP class is responsible for making sure that the server IP and port are valid.
    public ClientUDP(IPAddress serverIP, int serverPort) : base(RoleType.Client, serverIP, serverPort) { }
    public ClientUDP(string? configPath = null) : base(RoleType.Client, configPath) { }

    // An arbitrary message ID to keep track of messages
    private int _lastMessageId = new Random().Next(1, 100000);

    public void Start(List<DnsLookup> domainLookups)
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(ServerIP, ServerPort);
        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Create and send Hello
        SendHello(client, remoteEndPoint);

        // Receive and print Welcome from server
        ReceiveMessage(client);

        foreach (var lookupRequest in domainLookups)
        {
            var dnsRequestId = GetNextMessageId();

            // Create and send DNSLookup Message
            SendDNSLookup(client, remoteEndPoint, lookupRequest, dnsRequestId);

            // Receive and print DNSLookupReply from server
            ReceiveMessage(client);

            // Send Acknowledgment to Server
            SendAcknowledgment(client, remoteEndPoint, dnsRequestId);
        }

        // Send End message and receive End confirmation
        SendEnd(client, remoteEndPoint);
        ReceiveMessage(client);
    }

    private void SendHello(Socket client, IPEndPoint remoteEndPoint)
        => SendMessage(MessageType.Hello, client, remoteEndPoint, GetNextMessageId(), "Hello from client");

    private void SendDNSLookup(Socket client, IPEndPoint remoteEndPoint, DnsLookup lookupRequest, int dnsRequestId)
        => SendMessage(MessageType.DNSLookup, client, remoteEndPoint, dnsRequestId, JsonSerializer.Serialize(lookupRequest));

    private void SendAcknowledgment(Socket client, IPEndPoint remoteEndPoint, int msgId)
        => SendMessage(MessageType.Ack, client, remoteEndPoint, msgId, msgId.ToString());

    private void SendEnd(Socket client, IPEndPoint remoteEndPoint)
        => SendMessage(MessageType.End, client, remoteEndPoint, GetNextMessageId(), "End of DNSLookup");

    private void SendMessage(MessageType type, Socket client, IPEndPoint remoteEndPoint, int msgId = -1, string? content = null)
    {
        var message = new Message
        {
            MsgId = msgId == -1 ? GetNextMessageId() : msgId,
            MsgType = type,
            Content = content
        };

        Log(LogLevel.Information, message.Content!.ToString()!, message.MsgType, message.MsgId, remoteEndPoint: remoteEndPoint, direction: DirectionType.Out);

        var messageJson = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(messageJson);
        client.SendTo(buffer, remoteEndPoint);
    }

    private Message? ReceiveMessage(Socket client)
    {
        var buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        Message? message = null;
        string receivedMessage = string.Empty;

        try
        {
            int receivedBytes = client.ReceiveFrom(buffer, ref remoteEndPoint);
            receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            message = JsonSerializer.Deserialize<Message>(receivedMessage);
        }
        catch (SocketException e)
        {
            Log(LogLevel.Error, e.Message);
            return null;
        }

        if (message == null)
            Log(LogLevel.Warning, $"Unable to deserialize message: {receivedMessage}", remoteEndPoint: remoteEndPoint, direction: DirectionType.In);
        else
            Log(LogLevel.Information, message.Content!.ToString()!, message.MsgType, message.MsgId, remoteEndPoint, direction: DirectionType.In);

        return message;
    }

    private int GetNextMessageId()
    {
        if (_lastMessageId == int.MaxValue)
            _lastMessageId = 0;

        return ++_lastMessageId;
    }
}
