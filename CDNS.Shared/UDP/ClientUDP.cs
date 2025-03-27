using System.Net.Sockets;
using System.Net;
using System.Text.Json;

namespace CDNS.Shared.UDP;

public class ClientUDP : BaseUDP
{
    // The BaseUDP class is responsible for making sure that the server IP and port are valid.
    public ClientUDP(IPAddress serverIP, int serverPort) : base(RoleType.Client, serverIP, serverPort) { }
    public ClientUDP(string? configPath = null) : base(RoleType.Client, configPath) { }

    public void Start()
    {
        IPEndPoint ipEndPoint = new IPEndPoint(ServerIP, ServerPort);
        IPEndPoint sender = new IPEndPoint(ServerIP, ServerPort);

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

        //TODO: [Receive and print Welcome from server]

        // TODO: [Create and send DNSLookup Message]

        //TODO: [Receive and print DNSLookupReply from server]

        //TODO: [Send Acknowledgment to Server]

        // TODO: [Send next DNSLookup to server]
        // repeat the process until all DNSLoopkups (correct and incorrect onces) are sent to server and the replies with DNSLookupReply

        //TODO: [Receive and print End from server]
    }
}
