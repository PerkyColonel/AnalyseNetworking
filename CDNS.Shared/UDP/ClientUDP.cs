using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using CDNS.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CDNS.Shared.UDP;

public class ClientUDP : BaseUDP
{
    // The BaseUDP class is responsible for making sure that the server IP and port are valid.
    public ClientUDP(IPAddress serverIP, int serverPort) : base(RoleType.Client, serverIP, serverPort) { }
    public ClientUDP(string? configPath = null) : base(RoleType.Client, configPath) { }

    // An arbitrary message ID to keep track of messages
    private int _lastMessageId = new Random().Next(1, 100000);

    public void Start()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(ServerIP, ServerPort);
        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Create and send Hello
        SendHello(client, remoteEndPoint);

        // Receive and print Welcome from server
        ReceiveMessage(client);

        // menu
        bool stop = false;
        while (!stop)
        {
            Console.WriteLine($"");
            Console.WriteLine($"Kies een van de onderstaande opties: ");
            Console.WriteLine($"[1] preset 1");
            Console.WriteLine($"[2] preset 2");
            Console.WriteLine($"[3] preset 3");
            Console.WriteLine($"[4] Zelf een domein zoeken.");
            Console.WriteLine($"[5] stoppen");
            char option = Console.ReadKey().KeyChar;
            Console.WriteLine("");

            switch (option)
            {
                case '1':
                    UsePreset(client, remoteEndPoint, @"Configurations/Lookups1.json");
                    break;
                case '2':
                    UsePreset(client, remoteEndPoint, @"Configurations/Lookups2.json");
                    break;
                case '3':
                    UsePreset(client, remoteEndPoint, @"Configurations/Lookups3.json");
                    break;
                case '4':
                    SearchUrl(client, remoteEndPoint);
                    break;
                case '5':
                    stop = true;
                    break;
                default:
                    Console.WriteLine("Not an option");
                    break;
            }

        }

        // Send End message and receive End confirmation
        SendEnd(client, remoteEndPoint);
        ReceiveMessage(client);
    }

    private void UsePreset(Socket client, IPEndPoint ipEndPoint, string path)
    {
        List<string> dnsLookups = new List<string>();
        if (File.Exists(path))
        {
            string lookupData = File.ReadAllText(path);
            dnsLookups = JsonSerializer.Deserialize<List<string>>(lookupData);
        }

        

        // List of DNS lookups to perform

        foreach (var domain in dnsLookups)
        {
            var dnsRequestId = GetNextMessageId();

            // Create and send DNSLookup Message
            SendDNSLookup(client, ipEndPoint, domain, dnsRequestId);

            // Receive and print DNSLookupReply from server
            ReceiveMessage(client);
            
            // Send Acknowledgment to Server
            SendAcknowledgment(client, ipEndPoint, dnsRequestId);
        }
    }

    private void SendHello(Socket client, IPEndPoint remoteEndPoint) 
        => SendMessage(MessageType.Hello, client, remoteEndPoint, GetNextMessageId(), "Hello from client");

    private void SendDNSLookup(Socket client, IPEndPoint remoteEndPoint, string domain, int dnsRequestId) 
        => SendMessage(MessageType.DNSLookup, client, remoteEndPoint, dnsRequestId, domain);

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
        int receivedBytes = client.ReceiveFrom(buffer, ref remoteEndPoint);
        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);

        var message = JsonSerializer.Deserialize<Message>(receivedMessage);
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

    private void SearchUrl(Socket client, IPEndPoint ipEndPoint)
    {
        Console.WriteLine("Zoek een URL of typ: 'q' om te stoppen");
        string url = Console.ReadLine();
        if (url is null || url == "q")
        {
            return;
        }
        int dnsRequestId = GetNextMessageId();
        SendDNSLookup(client, ipEndPoint, url, dnsRequestId);
        ReceiveMessage(client);
        SendAcknowledgment(client, ipEndPoint, dnsRequestId);
    }
}
