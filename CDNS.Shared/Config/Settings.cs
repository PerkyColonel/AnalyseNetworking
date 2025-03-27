using CDNS.Shared.JsonConverters;
using System.Net;
using System.Text.Json.Serialization;

namespace CDNS.Shared.Config;

public class Settings
{
    public int Port { get; set; }
    [JsonConverter(typeof(IPAddressConverter))]
    public IPAddress? IP { get; set; }
}
