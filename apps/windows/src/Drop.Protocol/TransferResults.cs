namespace Drop.Protocol;

public sealed record FileTransferProgress(Guid FileId, long BytesTransferred, long TotalBytes);

public enum SendStage
{
    Connecting,
    WaitingForAcceptance,
    PreparingFile,
    Transferring,
    Completing
}

public sealed record SentFileResult(Guid FileId, string SourcePath, long BytesTransferred, string Sha256);

public sealed record SendSessionResult(Guid TransferId, IReadOnlyList<SentFileResult> Files);

public sealed record ReceivedFileResult(Guid FileId, string Path, long BytesReceived, string Sha256);

public sealed record ReceiveSessionResult(
    Guid TransferId,
    bool Success,
    IReadOnlyList<ReceivedFileResult> Files,
    string? ErrorCode = null);
