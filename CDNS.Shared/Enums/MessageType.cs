namespace CDNS.Shared;

// NOTE: This file is unchanged from the original specification. Only the namespace has changed.
public enum MessageType
{
    Hello,
    Welcome,
    DNSLookup,
    DNSLookupReply,
    DNSRecord,
    Ack,
    End,
    Error
}