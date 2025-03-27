using System.Net;

namespace CDNS.Shared.Config;

public class Settings
{
    public int Port { get; set; }
    public IPAddress? IP { get; set; }
}
