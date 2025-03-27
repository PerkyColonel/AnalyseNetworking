using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace CDNS.Shared.JsonConverters;

class IPAddressConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected string for IPAddress");
        }

        var ipString = reader.GetString();
        if (string.IsNullOrEmpty(ipString))
        {
            throw new JsonException("Expected string for IPAddress");
        }

        return IPAddress.Parse(ipString);
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}  