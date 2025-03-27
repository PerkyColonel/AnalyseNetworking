using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;
using CDNS.Shared.Models;

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
        IPEndPoint ipEndPoint = new IPEndPoint(ServerIP, ServerPort);
        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Create and send Hello
        SendHello(client, ipEndPoint);

        // Receive and print Welcome from server
        ReceiveWelcome(client);

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
                    UsePreset(client, ipEndPoint, @"Configurations/Lookups1.json");
                    break;
                case '2':
                    UsePreset(client, ipEndPoint, @"Configurations/Lookups2.json");
                    break;
                case '3':
                    UsePreset(client, ipEndPoint, @"Configurations/Lookups3.json");
                    break;
                case '4':
                    SearchUrl(client, ipEndPoint);
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
        SendEnd(client, ipEndPoint);
        ReceiveEnd(client);

        Console.ReadLine();
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
            ReceiveDNSLookupReply(client);

            // Send Acknowledgment to Server
            SendAcknowledgment(client, ipEndPoint, dnsRequestId);
        }
    }

    private void SendHello(Socket client, IPEndPoint ipEndPoint)
    {
        var helloMessage = new Message
        {
            MsgId = GetNextMessageId(),
            MsgType = MessageType.Hello,
            Content = "Hello from client"
        };
        SendMessage(helloMessage, ipEndPoint, client);
    }

    private void ReceiveWelcome(Socket client)
    {
        var buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        int receivedBytes = client.ReceiveFrom(buffer, ref remoteEndPoint);
        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Console.WriteLine($"Received: {receivedMessage}");
    }

    private void SendDNSLookup(Socket client, IPEndPoint ipEndPoint, string domain, int dnsRequestId)
    {
        var dnsLookupMessage = new Message
        {
            MsgId = dnsRequestId,
            MsgType = MessageType.DNSLookup,
            Content = domain
        };

        Console.WriteLine($"MsgId {dnsRequestId}: Looking up: {domain}");
        SendMessage(dnsLookupMessage, ipEndPoint, client);
    }

    private void ReceiveDNSLookupReply(Socket client)
    {
        var buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        int receivedBytes = client.ReceiveFrom(buffer, ref remoteEndPoint);
        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Console.WriteLine($"Received DNSLookupReply: {receivedMessage}");
    }

    private void SendAcknowledgment(Socket client, IPEndPoint ipEndPoint, int dnsRequestId)
    {
        var ackMessage = new Message
        {
            MsgId = dnsRequestId,
            MsgType = MessageType.Ack,
            Content = dnsRequestId
        };

        Console.WriteLine($"Sending Ack msgId: {dnsRequestId}");
        SendMessage(ackMessage, ipEndPoint, client);
    }

    private void SendEnd(Socket client, IPEndPoint ipEndPoint)
    {
        var endMessage = new Message
        {
            MsgId = GetNextMessageId(),
            MsgType = MessageType.End,
            Content = "End of DNSLookup"
        };

        Console.WriteLine("Sending End message");
        SendMessage(endMessage, ipEndPoint, client);
    }

    private void ReceiveEnd(Socket client)
    {
        var buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        int receivedBytes = client.ReceiveFrom(buffer, ref remoteEndPoint);
        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Console.WriteLine($"Received: {receivedMessage}");
    }

    private void SendMessage(Message message, EndPoint remoteEndPoint, Socket socket)
    {
        var messageJson = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(messageJson);
        socket.SendTo(buffer, remoteEndPoint);
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
        ReceiveDNSLookupReply(client);
        SendAcknowledgment(client, ipEndPoint, dnsRequestId);
    }
}
