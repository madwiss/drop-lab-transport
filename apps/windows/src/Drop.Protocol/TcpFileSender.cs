using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Drop.Transport;

namespace Drop.Protocol;

public sealed class TcpFileSender(DeviceInfo localDevice, ITransportConnector connector) : IFileSender
{
    private const int BufferSize = 128 * 1024;

    public Task<SendSessionResult> SendAsync(
        ITransportEndpoint endpoint,
        string sourcePath,
        string? remoteFileName = null,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<SendStage>? stageProgress = null,
        TransferTimeoutOptions? timeoutOptions = null) =>
        SendAsync(
            endpoint,
            [new FileTransferSource(sourcePath, remoteFileName)],
            progress,
            cancellationToken,
            stageProgress,
            timeoutOptions);

    public async Task<SendSessionResult> SendAsync(
        ITransportEndpoint endpoint,
        IReadOnlyList<FileTransferSource> files,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<SendStage>? stageProgress = null,
        TransferTimeoutOptions? timeoutOptions = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            throw new ArgumentException("At least one file is required.", nameof(files));
        }

        List<PreparedFile> preparedFiles = new(files.Count);

        foreach (FileTransferSource requestedFile in files)
        {
            ArgumentNullException.ThrowIfNull(requestedFile);
            ArgumentException.ThrowIfNullOrWhiteSpace(requestedFile.SourcePath);

            FileInfo source = new(requestedFile.SourcePath);
            if (!source.Exists)
            {
                throw new FileNotFoundException(
                    "Source file was not found.",
                    source.FullName);
            }

            string offeredName = string.IsNullOrWhiteSpace(requestedFile.RemoteFileName)
                ? source.Name
                : requestedFile.RemoteFileName!;

            preparedFiles.Add(new PreparedFile(
                Guid.NewGuid(),
                source.FullName,
                offeredName,
                source.Length));
        }

        long totalBytes = 0;
        foreach (PreparedFile file in preparedFiles)
        {
            totalBytes = checked(totalBytes + file.Size);
        }

        timeoutOptions ??= new TransferTimeoutOptions();

        Guid transferId = Guid.NewGuid();

        stageProgress?.Report(SendStage.Connecting);

        await using IReliableByteStream connection =
            await ConnectWithRetryAsync(
                endpoint,
                timeoutOptions,
                cancellationToken).ConfigureAwait(false);

        Stream stream = connection.Stream;

        await WriteControlAsync(
            stream,
            new
            {
                type = "HELLO",
                protocolVersion = ProtocolMessage.Version,
                device = DeviceJson(localDevice)
            },
            timeoutOptions.Handshake,
            cancellationToken,
            "HELLO").ConfigureAwait(false);

        using (await ReadControlAsync(
            stream,
            "HELLO_ACK",
            timeoutOptions.Handshake,
            cancellationToken).ConfigureAwait(false))
        {
        }

        await WriteControlAsync(
            stream,
            new
            {
                type = "OFFER",
                protocolVersion = ProtocolMessage.Version,
                transferId,
                files = preparedFiles
                    .Select(file => new
                    {
                        fileId = file.FileId,
                        name = file.Name,
                        size = file.Size
                    })
                    .ToArray(),
                totalBytes
            },
            timeoutOptions.Handshake,
            cancellationToken,
            "OFFER").ConfigureAwait(false);

        stageProgress?.Report(SendStage.WaitingForAcceptance);

        using (JsonDocument accept = await ReadControlAsync(
            stream,
            "ACCEPT",
            timeoutOptions.ReceiverAcceptance,
            cancellationToken,
            "receiver acceptance").ConfigureAwait(false))
        {
            ProtocolMessage.RequireTransfer(accept.RootElement, transferId);
        }

        List<SentFileResult> results = new(preparedFiles.Count);

        foreach (PreparedFile file in preparedFiles)
        {
            stageProgress?.Report(SendStage.PreparingFile);

            string hash = await ComputeSha256Async(
                file.SourcePath,
                cancellationToken).ConfigureAwait(false);

            await WriteControlAsync(
                stream,
                new
                {
                    type = "FILE_START",
                    protocolVersion = ProtocolMessage.Version,
                    transferId,
                    fileId = file.FileId,
                    name = file.Name,
                    size = file.Size,
                    sha256 = hash
                },
                timeoutOptions.Handshake,
                cancellationToken,
                "FILE_START").ConfigureAwait(false);

            stageProgress?.Report(SendStage.Transferring);

            await StreamFileAsync(
                stream,
                file.SourcePath,
                file.FileId,
                file.Size,
                progress,
                timeoutOptions.PayloadStall,
                cancellationToken).ConfigureAwait(false);

            stageProgress?.Report(SendStage.Completing);

            await WriteControlAsync(
                stream,
                new
                {
                    type = "FILE_END",
                    protocolVersion = ProtocolMessage.Version,
                    transferId,
                    fileId = file.FileId
                },
                timeoutOptions.Handshake,
                cancellationToken,
                "FILE_END").ConfigureAwait(false);

            using (JsonDocument result = await ReadControlAsync(
                stream,
                "FILE_RESULT",
                timeoutOptions.Handshake,
                cancellationToken).ConfigureAwait(false))
            {
                ProtocolMessage.RequireTransfer(result.RootElement, transferId);

                if (ProtocolMessage.RequiredGuid(
                    result.RootElement,
                    "fileId") != file.FileId)
                {
                    throw new TransferFailedException(
                        TransferFailureKind.Protocol,
                        "FILE_RESULT has an unexpected fileId.");
                }

                string status =
                    ProtocolMessage.RequiredString(
                        result.RootElement,
                        "status");

                if (status != "ok")
                {
                    throw new TransferFailedException(
                        TransferFailureKind.Protocol,
                        $"Receiver rejected the file: {status}.");
                }
            }

            results.Add(new SentFileResult(
                file.FileId,
                file.SourcePath,
                file.Size,
                hash));
        }

        await WriteControlAsync(
            stream,
            new
            {
                type = "COMPLETE",
                protocolVersion = ProtocolMessage.Version,
                transferId
            },
            timeoutOptions.Handshake,
            cancellationToken,
            "COMPLETE").ConfigureAwait(false);

        using (JsonDocument completeAck = await ReadControlAsync(
            stream,
            "COMPLETE_ACK",
            timeoutOptions.Handshake,
            cancellationToken).ConfigureAwait(false))
        {
            ProtocolMessage.RequireTransfer(
                completeAck.RootElement,
                transferId);
        }

        return new SendSessionResult(transferId, results);
    }
    private async Task<IReliableByteStream> ConnectWithRetryAsync(ITransportEndpoint endpoint, TransferTimeoutOptions options, CancellationToken cancellationToken)
    {
        TransferFailedException? lastFailure = null;
        for (int attempt = 0; attempt <= options.MaxConnectionRetries; attempt++)
        {
            try
            {
                return await TransferTimeout.RunAsync(token => connector.ConnectAsync(endpoint, token).AsTask(), options.Connect, cancellationToken, "connect").ConfigureAwait(false);
            }
            catch (TransferFailedException ex) when (ex.Kind is TransferFailureKind.Timeout or TransferFailureKind.Transport && attempt < options.MaxConnectionRetries)
            {
                lastFailure = ex;
            }
        }
        throw lastFailure ?? new TransferFailedException(TransferFailureKind.Transport, "Connection attempts were exhausted.");
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream file = OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash);
        }
        catch (OperationCanceledException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Cancelled, "Transfer cancelled.", ex);
        }
    }

    private static async Task StreamFileAsync(Stream destination, string path, Guid fileId, long expectedSize, IProgress<FileTransferProgress>? progress, TimeSpan stallTimeout, CancellationToken cancellationToken)
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
                int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new TransferFailedException(TransferFailureKind.Protocol, "Source file became shorter during transfer.");
                await TransferTimeout.RunAsync(async token => await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false), stallTimeout, cancellationToken, "payload write").ConfigureAwait(false);
                remaining -= read;
                transferred += read;
                progress?.Report(new FileTransferProgress(fileId, transferred, expectedSize));
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Cancelled, "Transfer cancelled.", ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record PreparedFile(
        Guid FileId,
        string SourcePath,
        string Name,
        long Size);
    private static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task WriteControlAsync(Stream stream, object message, TimeSpan timeout, CancellationToken cancellationToken, string phase) =>
        await TransferTimeout.RunAsync(token => ControlFrameCodec.WriteAsync(stream, ProtocolMessage.Create(message), token).AsTask(), timeout, cancellationToken, phase).ConfigureAwait(false);

    private static Task<JsonDocument> ReadControlAsync(Stream stream, string expectedType, TimeSpan timeout, CancellationToken cancellationToken, string? phase = null) =>
        TransferTimeout.RunAsync(token => ProtocolMessage.ReadExpectedAsync(stream, expectedType, token).AsTask(), timeout, cancellationToken, phase ?? expectedType);

    private static object DeviceJson(DeviceInfo device) => new { deviceId = device.DeviceId, name = device.Name, platform = device.Platform, appVersion = device.AppVersion, protocolVersion = ProtocolMessage.Version };
}

