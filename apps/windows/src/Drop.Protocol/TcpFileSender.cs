using System.Buffers;
using System.Security.Cryptography;
using Drop.Transport;

namespace Drop.Protocol;

/// <summary>
/// Sends one file over a Protocol v1 reliable-stream session. Discovery and UI are intentionally out of scope.
/// </summary>
public sealed class TcpFileSender(DeviceInfo localDevice, ITransportConnector connector)
{
    private const int BufferSize = 128 * 1024;

    public async Task<SendSessionResult> SendAsync(
        ITransportEndpoint endpoint,
        string sourcePath,
        string? remoteFileName = null,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<SendStage>? stageProgress = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        FileInfo source = new(sourcePath);
        if (!source.Exists)
        {
            throw new FileNotFoundException("Source file was not found.", source.FullName);
        }

        Guid transferId = Guid.NewGuid();
        Guid fileId = Guid.NewGuid();
        string offeredName = remoteFileName ?? source.Name;
        long size = source.Length;

        stageProgress?.Report(SendStage.Connecting);
        await using IReliableByteStream connection = await connector
            .ConnectAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);
        Stream stream = connection.Stream;

        await WriteAsync(stream, new
        {
            type = "HELLO",
            protocolVersion = ProtocolMessage.Version,
            device = DeviceJson(localDevice)
        }, cancellationToken).ConfigureAwait(false);

        using (await ProtocolMessage.ReadExpectedAsync(stream, "HELLO_ACK", cancellationToken)
            .ConfigureAwait(false))
        {
        }

        await WriteAsync(stream, new
        {
            type = "OFFER",
            protocolVersion = ProtocolMessage.Version,
            transferId,
            files = new[] { new { fileId, name = offeredName, size } },
            totalBytes = size
        }, cancellationToken).ConfigureAwait(false);

        stageProgress?.Report(SendStage.WaitingForAcceptance);
        using (var accept = await ProtocolMessage.ReadExpectedAsync(stream, "ACCEPT", cancellationToken)
            .ConfigureAwait(false))
        {
            ProtocolMessage.RequireTransfer(accept.RootElement, transferId);
        }

        stageProgress?.Report(SendStage.PreparingFile);
        string hash = await ComputeSha256Async(source.FullName, cancellationToken).ConfigureAwait(false);

        await WriteAsync(stream, new
        {
            type = "FILE_START",
            protocolVersion = ProtocolMessage.Version,
            transferId,
            fileId,
            name = offeredName,
            size,
            sha256 = hash
        }, cancellationToken).ConfigureAwait(false);

        stageProgress?.Report(SendStage.Transferring);
        await StreamFileAsync(stream, source.FullName, fileId, size, progress, cancellationToken)
            .ConfigureAwait(false);

        stageProgress?.Report(SendStage.Completing);
        await WriteAsync(stream, new
        {
            type = "FILE_END",
            protocolVersion = ProtocolMessage.Version,
            transferId,
            fileId
        }, cancellationToken).ConfigureAwait(false);

        using (var result = await ProtocolMessage.ReadExpectedAsync(stream, "FILE_RESULT", cancellationToken)
            .ConfigureAwait(false))
        {
            ProtocolMessage.RequireTransfer(result.RootElement, transferId);
            if (ProtocolMessage.RequiredGuid(result.RootElement, "fileId") != fileId)
            {
                throw new InvalidDataException("FILE_RESULT has an unexpected fileId.");
            }

            string status = ProtocolMessage.RequiredString(result.RootElement, "status");
            if (status != "ok")
            {
                throw new InvalidDataException($"Receiver rejected the file: {status}.");
            }
        }

        await WriteAsync(stream, new
        {
            type = "COMPLETE",
            protocolVersion = ProtocolMessage.Version,
            transferId
        }, cancellationToken).ConfigureAwait(false);

        using (var completeAck = await ProtocolMessage.ReadExpectedAsync(stream, "COMPLETE_ACK", cancellationToken)
            .ConfigureAwait(false))
        {
            ProtocolMessage.RequireTransfer(completeAck.RootElement, transferId);
        }

        return new SendSessionResult(
            transferId,
            [new SentFileResult(fileId, source.FullName, size, hash)]);
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream file = OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task StreamFileAsync(
        Stream destination,
        string path,
        Guid fileId,
        long expectedSize,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using FileStream source = OpenRead(path);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long remaining = expectedSize;
        long transferred = 0;
        progress?.Report(new FileTransferProgress(fileId, transferred, expectedSize));

        try
        {
            while (remaining > 0)
            {
                int requested = (int)Math.Min(buffer.Length, remaining);
                int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Source file became shorter during transfer.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                remaining -= read;
                transferred += read;
                progress?.Report(new FileTransferProgress(fileId, transferred, expectedSize));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        BufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static ValueTask WriteAsync(Stream stream, object message, CancellationToken cancellationToken) =>
        ControlFrameCodec.WriteAsync(stream, ProtocolMessage.Create(message), cancellationToken);

    private static object DeviceJson(DeviceInfo device) => new
    {
        deviceId = device.DeviceId,
        name = device.Name,
        platform = device.Platform,
        appVersion = device.AppVersion,
        protocolVersion = ProtocolMessage.Version
    };
}
