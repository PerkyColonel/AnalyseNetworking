using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using CDNS.Shared.Config;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace CDNS.Shared.UDP;

public abstract class BaseUDP
{
    public IPAddress ServerIP { get; private set; }
    public int ServerPort { get; private set; }
    protected RoleType Role { get; private set; }

    public BaseUDP(RoleType role, IPAddress serverIP, int serverPort)
    {
        Role = role;

        if (serverIP is null || serverPort < 9000)
            throw new InvalidOperationException("Server IP and port must be provided, server port must be 9000 or above.");

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

    protected void Log(LogLevel logLevel, string message, MessageType? type = null, int msgId = -1, EndPoint? remoteEndPoint = null, DirectionType direction = DirectionType.Out)
    {
        StringBuilder logBuilder = new StringBuilder();

        if (logLevel <= LogLevel.Debug) logBuilder.Append("[dim]");

        logBuilder.Append($"[{GetRoleColor(Role)}]{Role}[/] | ");
        logBuilder.Append($"[grey]{DateTime.Now.ToShortTimeString()}[/] | ");
        if (remoteEndPoint is not null)
            logBuilder.Append($"[darkorange3_1]{remoteEndPoint}[/] | ");
        if (msgId != -1)
            logBuilder.Append($"[grey]{msgId}[/] | ");
        if (type is not null)
            logBuilder.Append($"[{GetTypeColor(type)}]{type}-{direction}[/] | ");
        logBuilder.Append(message);

        if (logLevel <= LogLevel.Debug) logBuilder.Append("[/]");

        AnsiConsole.Console.MarkupLine(logBuilder.ToString());
    }

    private string GetRoleColor(RoleType role) => role switch
    {
        RoleType.Server => "blue",
        RoleType.Client => "green",
        _ => "yellow",
    };

    private string GetTypeColor(MessageType? type) => type switch
    {
        MessageType.Hello => "green",
        MessageType.Welcome => "blue",
        MessageType.DNSLookup => "yellow",
        MessageType.DNSLookupReply => "yellow",
        MessageType.DNSRecord => "yellow",
        MessageType.Ack => "green",
        MessageType.End => "red",
        MessageType.Error => "red",
        _ => "grey",
    };
}