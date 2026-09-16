using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Drop.Transport;

namespace Drop.Protocol;

/// <summary>
/// Receives one Protocol v1 file-transfer session over an established reliable byte stream.
/// </summary>
public sealed class TcpFileReceiver(DeviceInfo localDevice) : IFileReceiver
{
    private const int BufferSize = 128 * 1024;

    public async Task<ReceiveSessionResult> ReceiveAsync(
        IReliableByteStream connection,
        string destinationDirectory,
        Func<IncomingTransferOffer, CancellationToken, ValueTask<IncomingTransferDecision>> decide,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        TransferTimeoutOptions? timeoutOptions = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(decide);
        timeoutOptions ??= new TransferTimeoutOptions();

        string directory = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(directory);
        string? activePartialPath = null;
        string? activeFinalPath = null;

        await using (connection)
        {
            Stream stream = connection.Stream;
            try
            {
                DeviceInfo sender;
                using (JsonDocument hello = await ReadControlAsync(
                    stream, "HELLO", timeoutOptions.Handshake, cancellationToken).ConfigureAwait(false))
                {
                    sender = ReadDevice(hello.RootElement);
                }

                await WriteControlAsync(stream, new
                {
                    type = "HELLO_ACK",
                    protocolVersion = ProtocolMessage.Version,
                    device = DeviceJson(localDevice)
                }, timeoutOptions.Handshake, cancellationToken, "HELLO_ACK").ConfigureAwait(false);

                OfferedFile offered;
                Guid transferId;
                using (JsonDocument offer = await ReadControlAsync(
                    stream, "OFFER", timeoutOptions.Handshake, cancellationToken).ConfigureAwait(false))
                {
                    transferId = ProtocolMessage.RequiredGuid(offer.RootElement, "transferId");
                    offered = ReadSingleOfferedFile(offer.RootElement);
                    long totalBytes = ProtocolMessage.RequiredSize(offer.RootElement, "totalBytes");
                    if (totalBytes != offered.Size)
                    {
                        throw new InvalidDataException("OFFER totalBytes does not match the offered file.");
                    }
                }

                IncomingTransferOffer incomingOffer = new(
                    transferId, sender, offered.FileId, offered.Name, offered.Size);
                IncomingTransferDecision decision = await TransferTimeout.RunAsync(
                    token => decide(incomingOffer, token).AsTask(),
                    timeoutOptions.ReceiverAcceptance,
                    cancellationToken,
                    "receiver acceptance")
                    .ConfigureAwait(false);
                if (decision == IncomingTransferDecision.Decline)
                {
                    await WriteControlAsync(stream, new
                    {
                        type = "DECLINE",
                        protocolVersion = ProtocolMessage.Version,
                        transferId,
                        reason = "user_declined"
                    }, timeoutOptions.Handshake, cancellationToken, "DECLINE").ConfigureAwait(false);
                    return new ReceiveSessionResult(transferId, false, [], "DECLINED");
                }

                await WriteControlAsync(stream, new
                {
                    type = "ACCEPT",
                    protocolVersion = ProtocolMessage.Version,
                    transferId
                }, timeoutOptions.Handshake, cancellationToken, "ACCEPT").ConfigureAwait(false);

                string expectedHash;
                using (JsonDocument fileStart = await ReadControlAsync(
                    stream, "FILE_START", timeoutOptions.Handshake, cancellationToken).ConfigureAwait(false))
                {
                    JsonElement root = fileStart.RootElement;
                    ProtocolMessage.RequireTransfer(root, transferId);
                    if (ProtocolMessage.RequiredGuid(root, "fileId") != offered.FileId ||
                        ProtocolMessage.RequiredString(root, "name") != offered.Name ||
                        ProtocolMessage.RequiredSize(root) != offered.Size)
                    {
                        throw new InvalidDataException("FILE_START does not match the accepted offer.");
                    }

                    expectedHash = ProtocolMessage.RequiredString(root, "sha256");
                    if (!IsLowercaseSha256(expectedHash))
                    {
                        throw new InvalidDataException("FILE_START contains an invalid SHA-256 value.");
                    }
                }

                string safeName = DestinationFileNames.Sanitize(offered.Name);
                activePartialPath = Path.Combine(
                    directory,
                    $"{safeName}.{Guid.NewGuid():N}.drop-partial");
                DestinationFileNames.EnsureInsideDirectory(directory, activePartialPath);

                string actualHash = await ReceivePayloadAsync(
                    stream,
                    activePartialPath,
                    offered.FileId,
                    offered.Size,
                    progress,
                    timeoutOptions.PayloadStall,
                    cancellationToken)
                    .ConfigureAwait(false);

                using (JsonDocument fileEnd = await ReadControlAsync(
                    stream, "FILE_END", timeoutOptions.Handshake, cancellationToken).ConfigureAwait(false))
                {
                    ProtocolMessage.RequireTransfer(fileEnd.RootElement, transferId);
                    if (ProtocolMessage.RequiredGuid(fileEnd.RootElement, "fileId") != offered.FileId)
                    {
                        throw new InvalidDataException("FILE_END has an unexpected fileId.");
                    }
                }

                if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
                {
                    DeletePartial(activePartialPath);
                    activePartialPath = null;
                    await WriteFileResultAsync(
                        stream, transferId, offered.FileId, "hash_mismatch", timeoutOptions.Handshake, cancellationToken)
                        .ConfigureAwait(false);
                    return new ReceiveSessionResult(transferId, false, [], "HASH_MISMATCH");
                }

                await WriteFileResultAsync(stream, transferId, offered.FileId, "ok", timeoutOptions.Handshake, cancellationToken)
                    .ConfigureAwait(false);

                using (JsonDocument complete = await ReadControlAsync(
                    stream, "COMPLETE", timeoutOptions.Handshake, cancellationToken).ConfigureAwait(false))
                {
                    ProtocolMessage.RequireTransfer(complete.RootElement, transferId);
                }

                activeFinalPath = MoveToUniqueFinalPath(activePartialPath, directory, offered.Name);
                activePartialPath = null;

                await WriteControlAsync(stream, new
                {
                    type = "COMPLETE_ACK",
                    protocolVersion = ProtocolMessage.Version,
                    transferId
                }, timeoutOptions.Handshake, cancellationToken, "COMPLETE_ACK").ConfigureAwait(false);

                string finalPath = activeFinalPath;
                activeFinalPath = null;

                return new ReceiveSessionResult(
                    transferId,
                    true,
                    [new ReceivedFileResult(offered.FileId, finalPath, offered.Size, actualHash)]);
            }
            finally
            {
                if (activePartialPath is not null)
                {
                    DeletePartial(activePartialPath);
                }
                if (activeFinalPath is not null)
                {
                    DeletePartial(activeFinalPath);
                }
            }
        }
    }

    private static async Task<string> ReceivePayloadAsync(
        Stream source,
        string partialPath,
        Guid fileId,
        long size,
        IProgress<FileTransferProgress>? progress,
        TimeSpan stallTimeout,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long remaining = size;
        long received = 0;
        progress?.Report(new FileTransferProgress(fileId, received, size));

        try
        {
            await using FileStream destination = new(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (remaining > 0)
            {
                int requested = (int)Math.Min(buffer.Length, remaining);
                int read = await TransferTimeout.RunAsync(
                    async token => await source.ReadAsync(buffer.AsMemory(0, requested), token).ConfigureAwait(false),
                    stallTimeout,
                    cancellationToken,
                    "payload read").ConfigureAwait(false);
                if (read == 0)
                {
                    throw new TransferFailedException(
                        TransferFailureKind.Transport,
                        "Connection ended before the file payload was complete.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                remaining -= read;
                received += read;
                progress?.Report(new FileTransferProgress(fileId, received, size));
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch (OperationCanceledException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Cancelled, "Transfer cancelled.", ex);
        }
        catch (IOException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Transport, "Receive payload failed.", ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string MoveToUniqueFinalPath(
        string partialPath,
        string destinationDirectory,
        string incomingName)
    {
        while (true)
        {
            string finalPath = DestinationFileNames.ResolveUniquePath(destinationDirectory, incomingName);
            try
            {
                File.Move(partialPath, finalPath, overwrite: false);
                return finalPath;
            }
            catch (IOException) when (File.Exists(finalPath) || Directory.Exists(finalPath))
            {
                // Another transfer won the name race. Resolve the next suffix without overwriting it.
            }
        }
    }

    private static OfferedFile ReadSingleOfferedFile(JsonElement offer)
    {
        if (!offer.TryGetProperty("files", out JsonElement files) ||
            files.ValueKind != JsonValueKind.Array || files.GetArrayLength() != 1)
        {
            throw new InvalidDataException("This initial transfer core requires exactly one offered file.");
        }

        JsonElement file = files[0];
        return new OfferedFile(
            ProtocolMessage.RequiredGuid(file, "fileId"),
            ProtocolMessage.RequiredString(file, "name"),
            ProtocolMessage.RequiredSize(file));
    }

    private static DeviceInfo ReadDevice(JsonElement message)
    {
        if (!message.TryGetProperty("device", out JsonElement device) ||
            device.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("HELLO is missing device metadata.");
        }

        Guid deviceId = ProtocolMessage.RequiredGuid(device, "deviceId");
        string name = ProtocolMessage.RequiredString(device, "name");
        string platform = ProtocolMessage.RequiredString(device, "platform");
        string appVersion = ProtocolMessage.RequiredString(device, "appVersion");
        if (!device.TryGetProperty("protocolVersion", out JsonElement version) ||
            !version.TryGetInt32(out int value) || value != ProtocolMessage.Version)
        {
            throw new InvalidDataException("HELLO device metadata has an unsupported protocol version.");
        }

        return new DeviceInfo(deviceId, name, platform, appVersion);
    }

    private static bool IsLowercaseSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async ValueTask WriteFileResultAsync(
        Stream stream,
        Guid transferId,
        Guid fileId,
        string status,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        await WriteControlAsync(stream, new
        {
            type = "FILE_RESULT",
            protocolVersion = ProtocolMessage.Version,
            transferId,
            fileId,
            status
        }, timeout, cancellationToken, "FILE_RESULT").ConfigureAwait(false);

    private static async Task WriteControlAsync(
        Stream stream,
        object message,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string phase) =>
        await TransferTimeout.RunAsync(
            token => ControlFrameCodec.WriteAsync(stream, ProtocolMessage.Create(message), token).AsTask(),
            timeout,
            cancellationToken,
            phase).ConfigureAwait(false);

    private static Task<JsonDocument> ReadControlAsync(
        Stream stream,
        string expectedType,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        TransferTimeout.RunAsync(
            token => ProtocolMessage.ReadExpectedAsync(stream, expectedType, token).AsTask(),
            timeout,
            cancellationToken,
            expectedType);

    private static object DeviceJson(DeviceInfo device) => new
    {
        deviceId = device.DeviceId,
        name = device.Name,
        platform = device.Platform,
        appVersion = device.AppVersion,
        protocolVersion = ProtocolMessage.Version
    };

    private static void DeletePartial(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Preserve the original transfer error; best-effort cleanup can be retried by the host later.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original transfer error; best-effort cleanup can be retried by the host later.
        }
    }

    private sealed record OfferedFile(Guid FileId, string Name, long Size);
}

