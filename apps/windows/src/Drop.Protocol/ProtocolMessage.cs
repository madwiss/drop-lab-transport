using System.Text.Json;

namespace Drop.Protocol;

internal static class ProtocolMessage
{
    public const int Version = 1;

    public static JsonElement Create(object value) => JsonSerializer.SerializeToElement(value);

    public static async ValueTask<JsonDocument> ReadExpectedAsync(
        Stream stream,
        string expectedType,
        CancellationToken cancellationToken)
    {
        JsonDocument document = await ControlFrameCodec.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out JsonElement type) ||
                type.GetString() != expectedType)
            {
                throw new InvalidDataException($"Expected {expectedType} control message.");
            }

            if (!root.TryGetProperty("protocolVersion", out JsonElement version) ||
                !version.TryGetInt32(out int value) || value != Version)
            {
                throw new InvalidDataException("Unsupported or missing protocol version.");
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public static Guid RequiredGuid(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(property.GetString(), "D", out Guid value))
        {
            throw new InvalidDataException($"Missing or invalid {propertyName}.");
        }

        return value;
    }

    public static string RequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"Missing or invalid {propertyName}.");
        }

        return property.GetString()!;
    }

    public static long RequiredSize(JsonElement root, string propertyName = "size")
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property) ||
            !property.TryGetInt64(out long value) || value < 0)
        {
            throw new InvalidDataException($"Missing or invalid {propertyName}.");
        }

        return value;
    }

    public static void RequireTransfer(JsonElement root, Guid transferId)
    {
        if (RequiredGuid(root, "transferId") != transferId)
        {
            throw new InvalidDataException("Control message has an unexpected transferId.");
        }
    }
}
