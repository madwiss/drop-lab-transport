using System.Buffers.Binary;
using System.Text.Json;
using Drop.Transport;

namespace Drop.Protocol.Tests;

[TestClass]
public sealed class TimeoutRecoveryTests
{
    private static readonly DeviceInfo SenderDevice = new(
        Guid.Parse("33333333-3333-4333-8333-333333333333"), "Sender", "windows", "0.1.0");

    [TestMethod]
    public async Task ConnectTimeoutIsTypedAndBoundedAsync()
    {
        using TestFile source = new([1]);
        DelegateConnector connector = new((_, token) => HangAsync(token));
        TcpFileSender sender = new(SenderDevice, connector);

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                timeoutOptions: Options(connectMs: 30, maxRetries: 0)));

        Assert.AreEqual(TransferFailureKind.Timeout, failure.Kind);
        Assert.AreEqual(1, connector.Calls);
    }

    [TestMethod]
    public async Task CallerCancellationIsDistinctFromTimeoutAsync()
    {
        using TestFile source = new([1]);
        DelegateConnector connector = new((_, token) => HangAsync(token));
        TcpFileSender sender = new(SenderDevice, connector);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                cancellationToken: cancellation.Token,
                timeoutOptions: Options(connectMs: 500, maxRetries: 3)));

        Assert.AreEqual(TransferFailureKind.Cancelled, failure.Kind);
        Assert.AreEqual(1, connector.Calls);
    }

    [TestMethod]
    public async Task ConnectionRetryStopsAtConfiguredLimitAsync()
    {
        using TestFile source = new([1]);
        DelegateConnector connector = new((_, _) =>
            ValueTask.FromException<IReliableByteStream>(new IOException("unreachable")));
        TcpFileSender sender = new(SenderDevice, connector);

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                timeoutOptions: Options(maxRetries: 2)));

        Assert.AreEqual(TransferFailureKind.Transport, failure.Kind);
        Assert.AreEqual(3, connector.Calls);
    }

    [TestMethod]
    public async Task HandshakeTimeoutIsTypedAsync()
    {
        using TestFile source = new([1]);
        ScriptedStream stream = new(respondHello: false, respondAccept: false, stallPayload: false);
        TcpFileSender sender = new(SenderDevice, ConnectorFor(stream));

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                timeoutOptions: Options(handshakeMs: 30)));

        Assert.AreEqual(TransferFailureKind.Timeout, failure.Kind);
    }

    [TestMethod]
    public async Task ReceiverAcceptanceTimeoutIsTypedAsync()
    {
        using TestFile source = new([1]);
        ScriptedStream stream = new(respondHello: true, respondAccept: false, stallPayload: false);
        TcpFileSender sender = new(SenderDevice, ConnectorFor(stream));

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                timeoutOptions: Options(acceptanceMs: 30)));

        Assert.AreEqual(TransferFailureKind.Timeout, failure.Kind);
    }

    [TestMethod]
    public async Task ConnectionRetryCanRecoverBeforePayloadAsync()
    {
        using TestFile source = new([1, 2, 3, 4]);
        ScriptedStream stream = new(respondHello: true, respondAccept: true, stallPayload: false);
        DelegateConnector connector = new((attempt, _) => attempt == 1
            ? ValueTask.FromException<IReliableByteStream>(new IOException("first connection failed"))
            : ValueTask.FromResult<IReliableByteStream>(new FakeReliableByteStream(stream)));
        TcpFileSender sender = new(SenderDevice, connector);

        SendSessionResult result = await sender.SendAsync(
            DummyEndpoint.Instance,
            source.Path,
            timeoutOptions: Options(maxRetries: 2));

        Assert.AreEqual(2, connector.Calls);
        Assert.AreEqual(4L, result.Files.Single().BytesTransferred);
    }

    [TestMethod]
    public async Task PayloadStallTimesOutWithoutReconnectingOrReplayingAsync()
    {
        using TestFile source = new(new byte[1024]);
        ScriptedStream stream = new(respondHello: true, respondAccept: true, stallPayload: true);
        DelegateConnector connector = ConnectorFor(stream);
        TcpFileSender sender = new(SenderDevice, connector);

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(async () =>
            await sender.SendAsync(DummyEndpoint.Instance, source.Path,
                timeoutOptions: Options(payloadMs: 30, maxRetries: 3)));

        Assert.AreEqual(TransferFailureKind.Timeout, failure.Kind);
        Assert.AreEqual(1, connector.Calls);
        Assert.AreEqual(1, stream.PayloadWriteAttempts);
    }

    private static TransferTimeoutOptions Options(
        int connectMs = 500,
        int handshakeMs = 500,
        int acceptanceMs = 500,
        int payloadMs = 500,
        int maxRetries = 0) => new()
        {
            Connect = TimeSpan.FromMilliseconds(connectMs),
            Handshake = TimeSpan.FromMilliseconds(handshakeMs),
            ReceiverAcceptance = TimeSpan.FromMilliseconds(acceptanceMs),
            PayloadStall = TimeSpan.FromMilliseconds(payloadMs),
            MaxConnectionRetries = maxRetries
        };

    private static async ValueTask<IReliableByteStream> HangAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
        throw new InvalidOperationException("Unreachable.");
    }

    private static DelegateConnector ConnectorFor(ScriptedStream stream) => new((_, _) =>
        ValueTask.FromResult<IReliableByteStream>(new FakeReliableByteStream(stream)));

    private sealed class DummyEndpoint : ITransportEndpoint
    {
        public int DiscoveryPort => 0;
        public static DummyEndpoint Instance { get; } = new();
    }

    private sealed class DelegateConnector(
        Func<int, CancellationToken, ValueTask<IReliableByteStream>> connect) : ITransportConnector
    {
        private int _calls;
        public int Calls => _calls;

        public ValueTask<IReliableByteStream> ConnectAsync(
            ITransportEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            connect(Interlocked.Increment(ref _calls), cancellationToken);
    }

    private sealed class FakeReliableByteStream(Stream stream) : IReliableByteStream
    {
        public Stream Stream { get; } = stream;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScriptedStream(
        bool respondHello,
        bool respondAccept,
        bool stallPayload) : Stream
    {
        private readonly Queue<byte> _reads = new();
        private readonly SemaphoreSlim _readSignal = new(0);
        private readonly object _gate = new();
        private int? _controlLength;
        private long _payloadRemaining;
        private Guid _transferId;
        private Guid _fileId;

        public int PayloadWriteAttempts { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                lock (_gate)
                {
                    if (_reads.Count > 0)
                    {
                        int count = Math.Min(buffer.Length, _reads.Count);
                        for (int i = 0; i < count; i++)
                            buffer.Span[i] = _reads.Dequeue();
                        return count;
                    }
                }

                await _readSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_payloadRemaining > 0)
            {
                PayloadWriteAttempts++;
                if (stallPayload)
                {
                    await Task.Delay(TimeSpan.FromDays(1), cancellationToken).ConfigureAwait(false);
                    return;
                }

                _payloadRemaining -= buffer.Length;
                return;
            }

            if (_controlLength is null)
            {
                if (buffer.Length != ControlFrameCodec.LengthPrefixSize)
                    throw new InvalidDataException("Expected a control-frame length prefix.");
                _controlLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(buffer.Span));
                return;
            }

            if (buffer.Length != _controlLength.Value)
                throw new InvalidDataException("Unexpected control-frame payload length.");
            _controlLength = null;

            using JsonDocument message = JsonDocument.Parse(buffer);
            JsonElement root = message.RootElement;
            string type = root.GetProperty("type").GetString()!;
            switch (type)
            {
                case "HELLO" when respondHello:
                    Enqueue(new { type = "HELLO_ACK", protocolVersion = 1 });
                    break;
                case "OFFER":
                    _transferId = root.GetProperty("transferId").GetGuid();
                    if (respondAccept)
                        Enqueue(new { type = "ACCEPT", protocolVersion = 1, transferId = _transferId });
                    break;
                case "FILE_START":
                    _fileId = root.GetProperty("fileId").GetGuid();
                    _payloadRemaining = root.GetProperty("size").GetInt64();
                    break;
                case "FILE_END":
                    Enqueue(new
                    {
                        type = "FILE_RESULT",
                        protocolVersion = 1,
                        transferId = _transferId,
                        fileId = _fileId,
                        status = "ok"
                    });
                    break;
                case "COMPLETE":
                    Enqueue(new { type = "COMPLETE_ACK", protocolVersion = 1, transferId = _transferId });
                    break;
            }
        }

        private void Enqueue(object message)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message);
            byte[] prefix = new byte[ControlFrameCodec.LengthPrefixSize];
            BinaryPrimitives.WriteUInt32BigEndian(prefix, (uint)payload.Length);
            lock (_gate)
            {
                foreach (byte value in prefix) _reads.Enqueue(value);
                foreach (byte value in payload) _reads.Enqueue(value);
            }
            _readSignal.Release();
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class TestFile : IDisposable
    {
        public TestFile(byte[] content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"drop-timeout-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(Path, content);
        }

        public string Path { get; }
        public void Dispose() => File.Delete(Path);
    }
}

