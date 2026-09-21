using Drop.Transport;

namespace Drop.Protocol;

public interface IFileSender
{
    Task<SendSessionResult> SendAsync(
        ITransportEndpoint endpoint,
        string sourcePath,
        string? remoteFileName = null,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<SendStage>? stageProgress = null,
        TransferTimeoutOptions? timeoutOptions = null);

    Task<SendSessionResult> SendAsync(
        ITransportEndpoint endpoint,
        IReadOnlyList<FileTransferSource> files,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<SendStage>? stageProgress = null,
        TransferTimeoutOptions? timeoutOptions = null);
}