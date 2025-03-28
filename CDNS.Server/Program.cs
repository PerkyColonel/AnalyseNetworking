using CDNS.Server.UDP;

namespace CDNS.Server;

class Program
{
    static void Main(string[] args) => new ServerUDP().Start();
}