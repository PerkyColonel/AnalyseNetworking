using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Net;
using System.Text;

namespace CDNS.Shared.Helpers;

public static class LogHelper
{
    public static void Log(LogLevel logLevel, string message, MessageType? type = null, int msgId = -1, EndPoint? remoteEndPoint = null, DirectionType direction = DirectionType.Out, RoleType role = RoleType.Client)
    {
        StringBuilder logBuilder = new StringBuilder();

        if (logLevel <= LogLevel.Debug) logBuilder.Append("[dim]");

        logBuilder.Append($"[{GetRoleColor(role)}]{role}[/] | ");
        logBuilder.Append($"[grey]{DateTime.Now.ToShortTimeString()}[/] | ");
        if (remoteEndPoint is not null)
            logBuilder.Append($"[darkorange3_1]{remoteEndPoint}[/] | ");
        if (msgId != -1)
            logBuilder.Append($"[grey]{msgId}[/] | ");
        if (type is not null)
            logBuilder.Append($"[{GetTypeColor(type)}]{direction}[/] - [{GetTypeColor(type)}]{type}[/] | ");
        logBuilder.Append(message);

        if (logLevel <= LogLevel.Debug) logBuilder.Append("[/]");

        AnsiConsole.Console.MarkupLine(logBuilder.ToString());
    }

    private static string GetRoleColor(RoleType role) => role switch
    {
        RoleType.Server => "blue",
        RoleType.Client => "green",
        _ => "yellow",
    };

    private static string GetTypeColor(MessageType? type) => type switch
    {
        MessageType.Hello => "green",
        MessageType.Welcome => "blue",
        MessageType.DNSLookup => "yellow",
        MessageType.DNSLookupReply => "yellow",
        MessageType.Ack => "green",
        MessageType.End => "red",
        MessageType.Error => "red",
        _ => "grey",
    };
}
