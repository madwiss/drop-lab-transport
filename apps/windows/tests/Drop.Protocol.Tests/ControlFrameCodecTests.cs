using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Drop.Protocol.Tests;

[TestClass]
public sealed class ControlFrameCodecTests
{
    [TestMethod]
    public async Task ValidFrameRoundTripAsync()
    {
        using JsonDocument source = JsonDocument.Parse(
            """{"type":"HELLO","protocolVersion":1}""");
        await using MemoryStream stream = new();

        await ControlFrameCodec.WriteAsync(stream, source.RootElement);

        byte[] frame = stream.ToArray();
        Assert.AreEqual(frame.Length - ControlFrameCodec.LengthPrefixSize,
            BinaryPrimitives.ReadInt32BigEndian(frame));

        stream.Position = 0;
        using JsonDocument result = await ControlFrameCodec.ReadAsync(stream);

        Assert.AreEqual("HELLO", result.RootElement.GetProperty("type").GetString());
        Assert.AreEqual(1, result.RootElement.GetProperty("protocolVersion").GetInt32());
    }

    [TestMethod]
    public async Task UnicodeJsonIsDecodedAsync()
    {
        const string deviceName = "Nicolò-PC 📱 東京";
        byte[] payload = Encoding.UTF8.GetBytes(
            $$"""{"type":"HELLO","protocolVersion":1,"name":"{{deviceName}}"}""");
        byte[] frame = new byte[ControlFrameCodec.LengthPrefixSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, ControlFrameCodec.LengthPrefixSize);
        await using MemoryStream stream = new(frame);

        using JsonDocument result = await ControlFrameCodec.ReadAsync(stream);

        Assert.AreEqual(deviceName, result.RootElement.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task FragmentedReadsAreReassembledAsync()
    {
        using JsonDocument source = JsonDocument.Parse(
            """{"type":"ACCEPT","protocolVersion":1,"transferId":"bc69d089-9300-4a2d-b237-c131520a0001"}""");
        await using MemoryStream encoded = new();
        await ControlFrameCodec.WriteAsync(encoded, source.RootElement);
        await using FragmentedReadStream fragmented = new(encoded.ToArray(), maximumChunkSize: 1);

        using JsonDocument result = await ControlFrameCodec.ReadAsync(fragmented);

        Assert.AreEqual("ACCEPT", result.RootElement.GetProperty("type").GetString());
        Assert.IsGreaterThan(4, fragmented.ReadCount);
    }

    [TestMethod]
    public async Task OversizedFrameIsRejectedAsync()
    {
        byte[] lengthPrefix = new byte[ControlFrameCodec.LengthPrefixSize];
        BinaryPrimitives.WriteUInt32BigEndian(
            lengthPrefix,
            ControlFrameCodec.MaximumPayloadLength + 1u);
        await using MemoryStream stream = new(lengthPrefix);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await ControlFrameCodec.ReadAsync(stream));

        Assert.AreEqual(ControlFrameCodec.LengthPrefixSize, stream.Position);
    }

    [TestMethod]
    public async Task TruncatedLengthPrefixIsRejectedAsync()
    {
        await using MemoryStream stream = new([0x00, 0x00, 0x01]);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(
            async () => await ControlFrameCodec.ReadAsync(stream));
    }

    [TestMethod]
    public async Task TruncatedJsonPayloadIsRejectedAsync()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"type\":\"HELLO\"}");
        byte[] frame = new byte[ControlFrameCodec.LengthPrefixSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length + 1);
        payload.CopyTo(frame, ControlFrameCodec.LengthPrefixSize);
        await using MemoryStream stream = new(frame);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(
            async () => await ControlFrameCodec.ReadAsync(stream));
    }

    [TestMethod]
    public async Task ZeroLengthFrameIsRejectedAsync()
    {
        await using MemoryStream stream = new(new byte[ControlFrameCodec.LengthPrefixSize]);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await ControlFrameCodec.ReadAsync(stream));
    }

    [TestMethod]
    public async Task InvalidJsonPayloadIsRejectedAsync()
    {
        byte[] payload = "not-json"u8.ToArray();
        byte[] frame = new byte[ControlFrameCodec.LengthPrefixSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, ControlFrameCodec.LengthPrefixSize);
        await using MemoryStream stream = new(frame);

        InvalidDataException exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await ControlFrameCodec.ReadAsync(stream));

        Assert.IsInstanceOfType<JsonException>(exception.InnerException);
    }

    private sealed class FragmentedReadStream(byte[] bytes, int maximumChunkSize) : MemoryStream(bytes)
    {
        public int ReadCount { get; private set; }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunkSize)], cancellationToken);
        }
    }
}
