using System.Buffers.Binary;
using System.Text.Json;

namespace Drop.Protocol;

/// <summary>
/// Reads and writes Protocol v1 JSON control frames on a reliable byte stream.
/// </summary>
public static class ControlFrameCodec
{
    public const int LengthPrefixSize = sizeof(uint);
    public const int MaximumPayloadLength = 1024 * 1024;

    /// <summary>
    /// Writes one UTF-8 JSON document with its four-byte unsigned big-endian length prefix.
    /// </summary>
    public static async ValueTask WriteAsync(
        Stream stream,
        JsonElement json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(json);
        if (payload.Length is 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException(
                $"Control-frame payload length must be between 1 and {MaximumPayloadLength} bytes.");
        }

        byte[] lengthPrefix = new byte[LengthPrefixSize];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)payload.Length);

        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and parses one length-prefixed UTF-8 JSON document.
    /// </summary>
    public static async ValueTask<JsonDocument> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] lengthPrefix = new byte[LengthPrefixSize];
        await ReadExactlyAsync(stream, lengthPrefix, "control-frame length prefix", cancellationToken)
            .ConfigureAwait(false);

        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(lengthPrefix);
        if (payloadLength is 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException(
                $"Control-frame payload length must be between 1 and {MaximumPayloadLength} bytes.");
        }

        // Validate the bounded length before allocating a payload-sized buffer.
        byte[] payload = new byte[(int)payloadLength];
        await ReadExactlyAsync(stream, payload, "control-frame JSON payload", cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Control-frame payload is not valid JSON.", exception);
        }
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        string framePart,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer[totalRead..], cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException($"Stream ended before the {framePart} was complete.");
            }

            totalRead += bytesRead;
        }
    }
}

