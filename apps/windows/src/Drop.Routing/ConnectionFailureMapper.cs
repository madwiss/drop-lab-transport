using Drop.Protocol;

namespace Drop.Routing;

public static class ConnectionFailureMapper
{
    public static ConnectionFailure FromTransferFailure(TransferFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return failure.Kind switch
        {
            TransferFailureKind.Timeout => new(
                ConnectionFailureKind.ConnectionTimeout,
                IsRetryable: true,
                failure.Message),
            TransferFailureKind.Cancelled => new(
                ConnectionFailureKind.Cancelled,
                IsRetryable: false,
                failure.Message),
            TransferFailureKind.Transport => new(
                ConnectionFailureKind.TransportInterrupted,
                IsRetryable: true,
                failure.Message),
            TransferFailureKind.Protocol => new(
                ConnectionFailureKind.ProtocolError,
                IsRetryable: false,
                failure.Message),
            _ => new(ConnectionFailureKind.Unknown, IsRetryable: false, failure.Message)
        };
    }
}
