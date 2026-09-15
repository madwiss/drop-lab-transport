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

internal static class TransferTimeout
{
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken callerToken,
        string phase)
    {
        try
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            timeoutCts.CancelAfter(timeout);
            Task<T> task = operation(timeoutCts.Token);
            return await task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (TransferFailedException)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (callerToken.IsCancellationRequested)
        {
            throw new TransferFailedException(TransferFailureKind.Cancelled, "Transfer cancelled.", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Timeout, $"Transfer {phase} timed out.", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Protocol, $"Protocol failed during {phase}.", ex);
        }
        catch (IOException ex)
        {
            throw new TransferFailedException(TransferFailureKind.Transport, $"Transport failed during {phase}.", ex);
        }
    }

    public static async Task RunAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        CancellationToken callerToken,
        string phase) =>
        await RunAsync(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, timeout, callerToken, phase).ConfigureAwait(false);
}
