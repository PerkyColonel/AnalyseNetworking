namespace CDNS.Shared;


/// <summary>
/// Based on what TransIp offers
/// </summary>
public enum DnsRecordType
{
    A,
    AAAA,
    CNAME,
    MX,
    NS,
    TXT,
    SRV,
    SSHFP,
    TLSA,
    CAA,
    NAPTR,
    ALIAS,
    DS
}