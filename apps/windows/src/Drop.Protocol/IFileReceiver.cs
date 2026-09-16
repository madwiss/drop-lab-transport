using Drop.Transport;

namespace Drop.Protocol;

public interface IFileReceiver
{
    Task<ReceiveSessionResult> ReceiveAsync(
        IReliableByteStream connection,
        string destinationDirectory,
        Func<IncomingTransferOffer, CancellationToken, ValueTask<IncomingTransferDecision>> decide,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        TransferTimeoutOptions? timeoutOptions = null);
}