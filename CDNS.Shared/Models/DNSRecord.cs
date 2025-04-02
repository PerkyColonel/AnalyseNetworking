namespace CDNS.Shared.Models;

// NOTE: This file is unchanged from the original specification. Only the namespace has changed and the required attribute was added to suppress warnings.
public class DnsRecord
{
    public required string Type { get; set; } // Required to suppress warnings about nullability
    public required string Name { get; set; } // Required to suppress warnings about nullability
    public string? Value { get; set; }
    public int? TTL { get; set; }
    public int? Priority { get; set; } // Nullable for non-MX records
}
