namespace Drop.Protocol;

public sealed record TransferTimeoutOptions
{
    public TimeSpan Connect { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan Handshake { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan ReceiverAcceptance { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan PayloadStall { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxConnectionRetries { get; init; } = 2;
}

public enum TransferFailureKind
{
    Timeout,
    Cancelled,
    Transport,
    Protocol
}

public sealed class TransferFailedException : Exception
{
    public TransferFailedException(TransferFailureKind kind, string message, Exception? inner = null)
        : base(message, inner) => Kind = kind;

    public TransferFailureKind Kind { get; }
}
