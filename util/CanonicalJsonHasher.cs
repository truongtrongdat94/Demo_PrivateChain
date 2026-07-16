using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HashAnchorDemo;

public sealed record ProcessedRecord(
    string OriginalJson,
    string CanonicalJson,
    string Sha256);

public sealed class CanonicalJsonHasher
{
    public ProcessedRecord Process(JsonElement payload)
    {
        var originalJson = payload.GetRawText();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(payload, writer);
        }

        var canonicalJson = Encoding.UTF8.GetString(stream.ToArray());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return new ProcessedRecord(
            originalJson,
            canonicalJson,
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteNumberValue(element.GetDouble());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}");
        }
    }
}
