namespace CDNS.Shared.Models;

// NOTE: This file is unchanged from the original specification. Only the namespace has changed.
public class Message
{
    public int MsgId { get; set; }
    public MessageType MsgType { get; set; }
    public object? Content { get; set; }
}