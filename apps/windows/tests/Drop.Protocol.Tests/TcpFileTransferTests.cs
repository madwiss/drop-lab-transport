using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Drop.Transport;

namespace Drop.Protocol.Tests;

[TestClass]
public sealed class TcpFileTransferTests
{
    private static readonly DeviceInfo SenderDevice = new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"), "Sender", "windows", "0.1.0");
    private static readonly DeviceInfo ReceiverDevice = new(
        Guid.Parse("22222222-2222-4222-8222-222222222222"), "Receiver", "windows", "0.1.0");

    [TestMethod]
    public async Task SmallBinaryFileTransfersOverRealTcpLoopbackAsync()
    {
        using TestDirectory test = new();
        byte[] content = [0, 1, 2, 3, 0xff, 0x80, 0x42];
        string source = test.WriteSource("small.bin", content);

        TransferPair result = await TransferAsync(source, test.Destination);

        CollectionAssert.AreEqual(content, await File.ReadAllBytesAsync(result.ReceivedPath));
        Assert.IsTrue(result.ReceiveResult.Success);
        Assert.AreEqual(content.LongLength, result.SendResult.Files.Single().BytesTransferred);
    }

    [TestMethod]
    public async Task ZeroByteFileTransfersAsync()
    {
        using TestDirectory test = new();
        string source = test.WriteSource("empty.dat", []);

        TransferPair result = await TransferAsync(source, test.Destination);

        Assert.AreEqual(0L, new FileInfo(result.ReceivedPath).Length);
        Assert.AreEqual(
            Convert.ToHexStringLower(SHA256.HashData([])),
            result.ReceiveResult.Files.Single().Sha256);
    }

    [TestMethod]
    public async Task MultiMegabyteFileStreamsSuccessfullyAsync()
    {
        using TestDirectory test = new();
        byte[] content = new byte[5 * 1024 * 1024 + 37];
        RandomNumberGenerator.Fill(content);
        string source = test.WriteSource("large.bin", content);

        TransferPair result = await TransferAsync(source, test.Destination);

        Assert.AreEqual(content.LongLength, new FileInfo(result.ReceivedPath).Length);
        Assert.AreEqual(await HashFileAsync(source), await HashFileAsync(result.ReceivedPath));
    }

    [TestMethod]
    public async Task SourceAndDestinationSha256AreEqualAsync()
    {
        using TestDirectory test = new();
        string source = test.WriteSource("hash.bin", RandomNumberGenerator.GetBytes(64 * 1024 + 13));

        TransferPair result = await TransferAsync(source, test.Destination);
        string expected = await HashFileAsync(source);

        Assert.AreEqual(expected, await HashFileAsync(result.ReceivedPath));
        Assert.AreEqual(expected, result.SendResult.Files.Single().Sha256);
        Assert.AreEqual(expected, result.ReceiveResult.Files.Single().Sha256);
    }

    [TestMethod]
    public async Task IncorrectHashIsRejectedAndPartialIsRemovedAsync()
    {
        using TestDirectory test = new();
        byte[] payload = [1, 2, 3, 4, 5];
        await using ManualSession session = await ManualSession.StartAsync(test.Destination);
        await session.HandshakeAndStartFileAsync("wrong-hash.bin", payload.LongLength, new string('0', 64));

        await session.Stream.WriteAsync(payload);
        await session.WriteFileEndAsync();
        using JsonDocument resultFrame = await ControlFrameCodec.ReadAsync(session.Stream);
        ReceiveSessionResult receiverResult = await session.ReceiverTask;

        Assert.AreEqual("hash_mismatch", resultFrame.RootElement.GetProperty("status").GetString());
        Assert.IsFalse(receiverResult.Success);
        Assert.AreEqual("HASH_MISMATCH", receiverResult.ErrorCode);
        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    [TestMethod]
    public async Task TruncatedPayloadIsRejectedAndPartialIsRemovedAsync()
    {
        using TestDirectory test = new();
        await using ManualSession session = await ManualSession.StartAsync(test.Destination);
        await session.HandshakeAndStartFileAsync("truncated.bin", 100, new string('0', 64));

        await session.Stream.WriteAsync(new byte[10]);
        await session.Connection.DisposeAsync();

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(
            async () => await session.ReceiverTask);
        Assert.AreEqual(TransferFailureKind.Transport, failure.Kind);
        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    [TestMethod]
    public async Task PathTraversalFilenameRemainsInsideDestinationAsync()
    {
        using TestDirectory test = new();
        string source = test.WriteSource("source.bin", [7, 8, 9]);

        TransferPair result = await TransferAsync(source, test.Destination, "../../escaped.bin");

        Assert.AreEqual("escaped.bin", Path.GetFileName(result.ReceivedPath));
        Assert.AreEqual(
            Path.GetFullPath(test.Destination),
            Path.GetDirectoryName(Path.GetFullPath(result.ReceivedPath)));
        Assert.IsFalse(File.Exists(Path.Combine(test.Root, "escaped.bin")));
    }

    [TestMethod]
    public async Task DuplicateDestinationFilenameDoesNotOverwriteAsync()
    {
        using TestDirectory test = new();
        string existing = Path.Combine(test.Destination, "photo.jpg");
        await File.WriteAllBytesAsync(existing, [1, 1, 1]);
        string source = test.WriteSource("photo.jpg", [2, 2, 2]);

        TransferPair first = await TransferAsync(source, test.Destination);
        TransferPair second = await TransferAsync(source, test.Destination);

        Assert.AreEqual("photo (1).jpg", Path.GetFileName(first.ReceivedPath));
        Assert.AreEqual("photo (2).jpg", Path.GetFileName(second.ReceivedPath));
        CollectionAssert.AreEqual(new byte[] { 1, 1, 1 }, await File.ReadAllBytesAsync(existing));
    }

    [TestMethod]
    public async Task ReceiverCancellationDuringPayloadCleansPartialFileAsync()
    {
        using TestDirectory test = new();
        using CancellationTokenSource cancellation = new();
        await using ManualSession session = await ManualSession.StartAsync(test.Destination, cancellation.Token);
        await session.HandshakeAndStartFileAsync("cancel.bin", 10 * 1024 * 1024, new string('0', 64));
        await session.Stream.WriteAsync(new byte[1024]);

        await WaitForPartialFileAsync(test.Destination);
        cancellation.Cancel();

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(
            async () => await session.ReceiverTask);
        Assert.AreEqual(TransferFailureKind.Cancelled, failure.Kind);

        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    [TestMethod]
    public async Task ReceiverPayloadStallTimesOutAndRemovesPartialAsync()
    {
        using TestDirectory test = new();
        TransferTimeoutOptions timeouts = new()
        {
            Handshake = TimeSpan.FromSeconds(1),
            ReceiverAcceptance = TimeSpan.FromSeconds(1),
            PayloadStall = TimeSpan.FromMilliseconds(40)
        };
        await using ManualSession session = await ManualSession.StartAsync(
            test.Destination,
            timeoutOptions: timeouts);
        await session.HandshakeAndStartFileAsync("stall.bin", 1024, new string('0', 64));

        TransferFailedException failure = await Assert.ThrowsAsync<TransferFailedException>(
            async () => await session.ReceiverTask);

        Assert.AreEqual(TransferFailureKind.Timeout, failure.Kind);
        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    [TestMethod]
    public async Task ReceiverDoesNotExposeFinalFileUntilCompleteAsync()
    {
        using TestDirectory test = new();
        byte[] payload = [9, 8, 7, 6];
        string hash = Convert.ToHexStringLower(SHA256.HashData(payload));
        await using ManualSession session = await ManualSession.StartAsync(test.Destination);
        await session.HandshakeAndStartFileAsync("pending.bin", payload.Length, hash);

        await session.Stream.WriteAsync(payload);
        await session.WriteFileEndAsync();
        using JsonDocument resultFrame = await ControlFrameCodec.ReadAsync(session.Stream);
        Assert.AreEqual("ok", resultFrame.RootElement.GetProperty("status").GetString());

        await session.Connection.DisposeAsync();
        await Assert.ThrowsAsync<Exception>(async () => await session.ReceiverTask);

        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    [TestMethod]
    public async Task DeclinedOfferSendsDeclineAndCreatesNoFileAsync()
    {
        using TestDirectory test = new();
        IncomingTransferOffer? presentedOffer = null;
        await using ManualSession session = await ManualSession.StartAsync(
            test.Destination,
            decide: (offer, _) =>
            {
                presentedOffer = offer;
                return ValueTask.FromResult(IncomingTransferDecision.Decline);
            });

        using JsonDocument response = await session.HandshakeAndOfferAsync("declined.bin", 123);
        ReceiveSessionResult result = await session.ReceiverTask;

        Assert.AreEqual("DECLINE", response.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("user_declined", response.RootElement.GetProperty("reason").GetString());
        Assert.AreEqual("Sender", presentedOffer?.Sender.Name);
        Assert.AreEqual("declined.bin", presentedOffer?.FileName);
        Assert.AreEqual(123L, presentedOffer?.FileSize);
        Assert.IsFalse(result.Success);
        Assert.AreEqual("DECLINED", result.ErrorCode);
        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    private static async Task<TransferPair> TransferAsync(
        string source,
        string destination,
        string? remoteName = null)
    {
        await using TcpTransportListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        TcpTransportEndpoint endpoint = (TcpTransportEndpoint)listener.LocalEndpoint;
        TcpFileReceiver receiver = new(ReceiverDevice);
        Task<ReceiveSessionResult> receiveTask = Task.Run(async () =>
        {
            IReliableByteStream accepted = await listener.AcceptAsync();
            return await receiver.ReceiveAsync(accepted, destination, AcceptOffer);
        });

        TcpFileSender sender = new(SenderDevice, new TcpTransportConnector());
        SendSessionResult sendResult = await sender.SendAsync(endpoint, source, remoteName);
        ReceiveSessionResult receiveResult = await receiveTask;
        return new TransferPair(sendResult, receiveResult, receiveResult.Files.Single().Path);
    }

    private static async Task<string> HashFileAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }

    private static async Task WaitForPartialFileAsync(string destination)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!Directory.EnumerateFiles(destination, "*.drop-partial").Any())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed record TransferPair(
        SendSessionResult SendResult,
        ReceiveSessionResult ReceiveResult,
        string ReceivedPath);

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), $"drop-tests-{Guid.NewGuid():N}");
            Destination = Path.Combine(Root, "destination");
            Directory.CreateDirectory(Destination);
        }

        public string Root { get; }

        public string Destination { get; }

        public string WriteSource(string name, byte[] content)
        {
            string sourceDirectory = Path.Combine(Root, "source");
            Directory.CreateDirectory(sourceDirectory);
            string path = Path.Combine(sourceDirectory, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class ManualSession : IAsyncDisposable
    {
        private ManualSession(
            TcpTransportListener listener,
            IReliableByteStream connection,
            Task<ReceiveSessionResult> receiverTask)
        {
            Listener = listener;
            Connection = connection;
            ReceiverTask = receiverTask;
        }

        public TcpTransportListener Listener { get; }

        public IReliableByteStream Connection { get; }

        public Stream Stream => Connection.Stream;

        public Task<ReceiveSessionResult> ReceiverTask { get; }

        public Guid TransferId { get; } = Guid.NewGuid();

        public Guid FileId { get; } = Guid.NewGuid();

        public static async Task<ManualSession> StartAsync(
            string destination,
            CancellationToken receiverCancellation = default,
            Func<IncomingTransferOffer, CancellationToken, ValueTask<IncomingTransferDecision>>? decide = null,
            TransferTimeoutOptions? timeoutOptions = null)
        {
            TcpTransportListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            ValueTask<IReliableByteStream> acceptTask = listener.AcceptAsync();
            IReliableByteStream client = await new TcpTransportConnector().ConnectAsync(listener.LocalEndpoint);
            IReliableByteStream accepted = await acceptTask;
            TcpFileReceiver receiver = new(ReceiverDevice);
            Task<ReceiveSessionResult> receiverTask = receiver.ReceiveAsync(
                accepted, destination, decide ?? AcceptOffer, progress: null,
                cancellationToken: receiverCancellation,
                timeoutOptions: timeoutOptions);
            return new ManualSession(listener, client, receiverTask);
        }

        public async Task HandshakeAndStartFileAsync(string name, long size, string sha256)
        {
            using JsonDocument accept = await HandshakeAndOfferAsync(name, size);
            Assert.AreEqual("ACCEPT", accept.RootElement.GetProperty("type").GetString());

            await WriteAsync(new
            {
                type = "FILE_START",
                protocolVersion = 1,
                transferId = TransferId,
                fileId = FileId,
                name,
                size,
                sha256
            });
        }

        public async Task<JsonDocument> HandshakeAndOfferAsync(string name, long size)
        {
            await WriteAsync(new
            {
                type = "HELLO",
                protocolVersion = 1,
                device = new
                {
                    deviceId = SenderDevice.DeviceId,
                    name = SenderDevice.Name,
                    platform = SenderDevice.Platform,
                    appVersion = SenderDevice.AppVersion,
                    protocolVersion = 1
                }
            });
            using JsonDocument helloAck = await ControlFrameCodec.ReadAsync(Stream);
            Assert.AreEqual("HELLO_ACK", helloAck.RootElement.GetProperty("type").GetString());

            await WriteAsync(new
            {
                type = "OFFER",
                protocolVersion = 1,
                transferId = TransferId,
                files = new[] { new { fileId = FileId, name, size } },
                totalBytes = size
            });
            return await ControlFrameCodec.ReadAsync(Stream);
        }

        public Task WriteFileEndAsync() => WriteAsync(new
        {
            type = "FILE_END",
            protocolVersion = 1,
            transferId = TransferId,
            fileId = FileId
        });

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            await Listener.DisposeAsync();
        }

        private async Task WriteAsync(object message) =>
            await ControlFrameCodec.WriteAsync(Stream, JsonSerializer.SerializeToElement(message));
    }

    private static ValueTask<IncomingTransferDecision> AcceptOffer(
        IncomingTransferOffer offer,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(IncomingTransferDecision.Accept);
}

