using System.Net;
using System.Text.Json;
using CDNS.Shared.Config;

namespace CDNS.Shared.UDP;

public abstract class BaseUDP
{
    public IPAddress ServerIP { get; private set; }
    public int ServerPort { get; private set; }
    protected RoleType Role { get; private set; }

    public BaseUDP(RoleType role, IPAddress serverIP, int serverPort)
    {
        if (serverIP is null || serverPort < 9000)
            throw new InvalidOperationException("Server IP and port must be provided, server port must be 9000 or above.");

        ServerIP = serverIP;
        ServerPort = serverPort;
        Role = role;
    }

    public BaseUDP(RoleType role, string? configPath)
    {
        var roleName = role.ToString();

        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = $"Configurations/{roleName.ToLower()}SettingsDefault.json";
            Console.WriteLine($"No {roleName.ToLower()} configuration path provided. Using default path: {configPath}.");
        }

        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(configPath));

        if (settings is null)
            throw new InvalidOperationException($"Failed to read {roleName.ToLower()} settings from {configPath}!");

        if (settings.IP == null)
            throw new InvalidOperationException($"Server IP must be provided.");

        ServerIP = settings.IP;
        ServerPort = settings.Port;
        Role = role;
    }
}