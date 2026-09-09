using System.Buffers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace Drop.Protocol;

/// <summary>
/// Receives one Protocol v1 file-transfer session over an established TCP connection.
/// </summary>
public sealed class TcpFileReceiver(DeviceInfo localDevice)
{
    private const int BufferSize = 128 * 1024;

    public async Task<ReceiveSessionResult> ReceiveAsync(
        TcpClient client,
        string destinationDirectory,
        Func<IncomingTransferOffer, CancellationToken, ValueTask<IncomingTransferDecision>> decide,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(decide);

        string directory = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(directory);
        string? activePartialPath = null;

        using (client)
        await using (NetworkStream stream = client.GetStream())
        {
            try
            {
                DeviceInfo sender;
                using (JsonDocument hello = await ProtocolMessage.ReadExpectedAsync(
                    stream, "HELLO", cancellationToken).ConfigureAwait(false))
                {
                    sender = ReadDevice(hello.RootElement);
                }

                await WriteAsync(stream, new
                {
                    type = "HELLO_ACK",
                    protocolVersion = ProtocolMessage.Version,
                    device = DeviceJson(localDevice)
                }, cancellationToken).ConfigureAwait(false);

                OfferedFile offered;
                Guid transferId;
                using (JsonDocument offer = await ProtocolMessage.ReadExpectedAsync(
                    stream, "OFFER", cancellationToken).ConfigureAwait(false))
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
                IncomingTransferDecision decision = await decide(incomingOffer, cancellationToken)
                    .ConfigureAwait(false);
                if (decision == IncomingTransferDecision.Decline)
                {
                    await WriteAsync(stream, new
                    {
                        type = "DECLINE",
                        protocolVersion = ProtocolMessage.Version,
                        transferId,
                        reason = "user_declined"
                    }, cancellationToken).ConfigureAwait(false);
                    return new ReceiveSessionResult(transferId, false, [], "DECLINED");
                }

                await WriteAsync(stream, new
                {
                    type = "ACCEPT",
                    protocolVersion = ProtocolMessage.Version,
                    transferId
                }, cancellationToken).ConfigureAwait(false);

                string expectedHash;
                using (JsonDocument fileStart = await ProtocolMessage.ReadExpectedAsync(
                    stream, "FILE_START", cancellationToken).ConfigureAwait(false))
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
                    stream, activePartialPath, offered.FileId, offered.Size, progress, cancellationToken)
                    .ConfigureAwait(false);

                using (JsonDocument fileEnd = await ProtocolMessage.ReadExpectedAsync(
                    stream, "FILE_END", cancellationToken).ConfigureAwait(false))
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
                        stream, transferId, offered.FileId, "hash_mismatch", cancellationToken)
                        .ConfigureAwait(false);
                    return new ReceiveSessionResult(transferId, false, [], "HASH_MISMATCH");
                }

                string finalPath = MoveToUniqueFinalPath(activePartialPath, directory, offered.Name);
                activePartialPath = null;

                await WriteFileResultAsync(stream, transferId, offered.FileId, "ok", cancellationToken)
                    .ConfigureAwait(false);

                using (JsonDocument complete = await ProtocolMessage.ReadExpectedAsync(
                    stream, "COMPLETE", cancellationToken).ConfigureAwait(false))
                {
                    ProtocolMessage.RequireTransfer(complete.RootElement, transferId);
                }

                await WriteAsync(stream, new
                {
                    type = "COMPLETE_ACK",
                    protocolVersion = ProtocolMessage.Version,
                    transferId
                }, cancellationToken).ConfigureAwait(false);

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
            }
        }
    }

    private static async Task<string> ReceivePayloadAsync(
        Stream source,
        string partialPath,
        Guid fileId,
        long size,
        IProgress<FileTransferProgress>? progress,
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
                int read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Connection ended before the file payload was complete.");
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
        CancellationToken cancellationToken) =>
        await WriteAsync(stream, new
        {
            type = "FILE_RESULT",
            protocolVersion = ProtocolMessage.Version,
            transferId,
            fileId,
            status
        }, cancellationToken).ConfigureAwait(false);

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
