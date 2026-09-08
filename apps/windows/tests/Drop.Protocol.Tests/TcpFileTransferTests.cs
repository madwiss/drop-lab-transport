using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

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
        session.Client.Client.Shutdown(SocketShutdown.Send);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await session.ReceiverTask);
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

        try
        {
            await session.ReceiverTask;
            Assert.Fail("Receiver should have observed cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.IsEmpty(Directory.GetFiles(test.Destination));
    }

    private static async Task<TransferPair> TransferAsync(
        string source,
        string destination,
        string? remoteName = null)
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
            TcpFileReceiver receiver = new(ReceiverDevice);
            Task<ReceiveSessionResult> receiveTask = Task.Run(async () =>
            {
                TcpClient accepted = await listener.AcceptTcpClientAsync();
                return await receiver.ReceiveAsync(accepted, destination);
            });

            TcpFileSender sender = new(SenderDevice);
            SendSessionResult sendResult = await sender.SendAsync(endpoint, source, remoteName);
            ReceiveSessionResult receiveResult = await receiveTask;
            return new TransferPair(sendResult, receiveResult, receiveResult.Files.Single().Path);
        }
        finally
        {
            listener.Stop();
        }
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
            TcpListener listener,
            TcpClient client,
            NetworkStream stream,
            Task<ReceiveSessionResult> receiverTask)
        {
            Listener = listener;
            Client = client;
            Stream = stream;
            ReceiverTask = receiverTask;
        }

        public TcpListener Listener { get; }

        public TcpClient Client { get; }

        public NetworkStream Stream { get; }

        public Task<ReceiveSessionResult> ReceiverTask { get; }

        public Guid TransferId { get; } = Guid.NewGuid();

        public Guid FileId { get; } = Guid.NewGuid();

        public static async Task<ManualSession> StartAsync(
            string destination,
            CancellationToken receiverCancellation = default)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            TcpClient client = new(AddressFamily.InterNetwork);
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            TcpClient accepted = await acceptTask;
            TcpFileReceiver receiver = new(ReceiverDevice);
            Task<ReceiveSessionResult> receiverTask = receiver.ReceiveAsync(
                accepted, destination, progress: null, cancellationToken: receiverCancellation);
            return new ManualSession(listener, client, client.GetStream(), receiverTask);
        }

        public async Task HandshakeAndStartFileAsync(string name, long size, string sha256)
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
            using JsonDocument accept = await ControlFrameCodec.ReadAsync(Stream);
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

        public Task WriteFileEndAsync() => WriteAsync(new
        {
            type = "FILE_END",
            protocolVersion = 1,
            transferId = TransferId,
            fileId = FileId
        });

        public async ValueTask DisposeAsync()
        {
            await Stream.DisposeAsync();
            Client.Dispose();
            Listener.Stop();
        }

        private async Task WriteAsync(object message) =>
            await ControlFrameCodec.WriteAsync(Stream, JsonSerializer.SerializeToElement(message));
    }
}
