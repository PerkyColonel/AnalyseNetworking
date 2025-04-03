using CDNS.Shared.Config;
using CDNS.Shared.Helpers;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CDNS.Shared.UDP;

public abstract class BaseUDP
{
    public IPAddress ServerIP { get; private set; }
    public int ServerPort { get; private set; }
    protected RoleType Role { get; private set; }

    public BaseUDP(RoleType role, IPAddress serverIP, int serverPort)
    {
        Role = role;

        if (serverIP is null || serverPort < 5000)
            throw new InvalidOperationException("Server IP and port must be provided, server port must be 5000 or above.");

        ServerIP = serverIP;
        ServerPort = serverPort;
    }

    public BaseUDP(RoleType role, string? configPath)
    {
        Role = role;

        var roleName = role.ToString();

        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = $"Configurations/{roleName.ToLower()}SettingsDefault.json";
            Log(LogLevel.Information, $"No {roleName.ToLower()} configuration path provided. Using default path: {configPath}.");
        }

        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(configPath));

        if (settings is null)
            throw new InvalidOperationException($"Failed to read {roleName.ToLower()} settings from {configPath}!");

        if (settings.IP == null)
            throw new InvalidOperationException($"Server IP must be provided.");

        ServerIP = settings.IP;
        ServerPort = settings.Port;
    }

    public void Log(LogLevel logLevel, string message, MessageType? type = null, int msgId = -1, EndPoint? remoteEndPoint = null, DirectionType direction = DirectionType.Out)
        => LogHelper.Log(logLevel, message, type, msgId, remoteEndPoint, direction, Role);
}