using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{

    //TODO: [Deserialize Setting.json]
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);


    public static void start()
    {

        //TODO: [Create endpoints and socket]
        IPAddress ClientIP = IPAddress.Parse(setting.ClientIPAddress);
        int ClientPort = setting.ClientPortNumber;
        IPAddress ServerIP = IPAddress.Parse(setting.ServerIPAddress);
        int ServerPort = setting.ServerPortNumber;


        IPEndPoint ipEndPoint = new IPEndPoint(ServerIP, ServerPort);
        IPEndPoint sender = new IPEndPoint(ClientIP, ClientPort);

        Socket client;



        try
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }



        //TODO: [Create and send HELLO]
        Message msg = new Message();
        msg.MsgId = 1;
        msg.MsgType = MessageType.Hello;
        string message = "Hello from client";
        msg.Content = message;

        client.SendTo(JsonSerializer.Ser);
        
        



        //TODO: [Receive and print Welcome from server]


        // TODO: [Create and send DNSLookup Message]


        //TODO: [Receive and print DNSLookupReply from server]


        //TODO: [Send Acknowledgment to Server]

        // TODO: [Send next DNSLookup to server]
        // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

        //TODO: [Receive and print End from server]





    }
}