namespace Drop.Routing;

public enum ConnectionLifecycleState
{
    SelectingRoute = 0,
    Connecting = 1,
    Connected = 2,
    Transferring = 3,
    Recovering = 4,
    RetryWaiting = 5,
    Failed = 6,
    Cancelled = 7,
    Completed = 8
}

public enum ConnectionFailureKind
{
    NoEligibleRoute = 0,
    RouteUnavailable = 1,
    UnsupportedTransport = 2,
    ConnectionTimeout = 3,
    ConnectionRefused = 4,
    AuthenticationFailed = 5,
    TransportInterrupted = 6,
    ProtocolError = 7,
    RetryExhausted = 8,
    Unknown = 9
}

public sealed record ConnectionFailure(
    ConnectionFailureKind Kind,
    bool IsRetryable,
    string? Diagnostic = null);

public sealed record RetryMetadata
{
    public RetryMetadata(
        int Attempt,
        int MaxAttempts,
        TimeSpan Delay,
        DateTimeOffset? RetryNotBefore = null)
    {
        if (Attempt < 0)
            throw new ArgumentOutOfRangeException(nameof(Attempt));
        if (MaxAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts));
        if (Attempt > MaxAttempts)
            throw new ArgumentOutOfRangeException(nameof(Attempt), "Attempt cannot exceed MaxAttempts.");
        if (Delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Delay));

        this.Attempt = Attempt;
        this.MaxAttempts = MaxAttempts;
        this.Delay = Delay;
        this.RetryNotBefore = RetryNotBefore;
    }

    public int Attempt { get; }

    public int MaxAttempts { get; }

    public TimeSpan Delay { get; }

    public DateTimeOffset? RetryNotBefore { get; }

    public bool HasAttemptsRemaining => Attempt < MaxAttempts;

    public RetryMetadata Next(TimeSpan delay, DateTimeOffset? retryNotBefore = null)
    {
        if (!HasAttemptsRemaining)
            throw new InvalidOperationException("No retry attempts remain.");

        return new RetryMetadata(Attempt + 1, MaxAttempts, delay, retryNotBefore);
    }
}

public sealed record ConnectionSessionState(
    ConnectionLifecycleState State,
    ConnectionCandidate? Candidate = null,
    ConnectionFailure? Failure = null,
    RetryMetadata? Retry = null)
{
    public bool IsTerminal => State is ConnectionLifecycleState.Failed
        or ConnectionLifecycleState.Cancelled
        or ConnectionLifecycleState.Completed;

    public static ConnectionSessionState Selecting() =>
        new(ConnectionLifecycleState.SelectingRoute);

    public ConnectionSessionState Connecting(ConnectionCandidate candidate)
    {
        EnsureState(ConnectionLifecycleState.SelectingRoute, ConnectionLifecycleState.Recovering);
        ArgumentNullException.ThrowIfNull(candidate);
        return new(ConnectionLifecycleState.Connecting, candidate, Retry: Retry);
    }

    public ConnectionSessionState Connected()
    {
        EnsureState(ConnectionLifecycleState.Connecting);
        return this with { State = ConnectionLifecycleState.Connected, Failure = null };
    }

    public ConnectionSessionState Transferring()
    {
        EnsureState(ConnectionLifecycleState.Connected);
        return this with { State = ConnectionLifecycleState.Transferring };
    }

    public ConnectionSessionState Recover(ConnectionFailure failure, RetryMetadata retry)
    {
        EnsureState(ConnectionLifecycleState.Connecting, ConnectionLifecycleState.Connected, ConnectionLifecycleState.Transferring);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(retry);
        if (!failure.IsRetryable)
            throw new InvalidOperationException("A non-retryable failure cannot enter recovery.");
        if (!retry.HasAttemptsRemaining)
            throw new InvalidOperationException("Recovery requires a remaining retry attempt.");

        return new(ConnectionLifecycleState.Recovering, Candidate, failure, retry);
    }

    public ConnectionSessionState WaitForRetry(TimeSpan delay, DateTimeOffset? retryNotBefore = null)
    {
        EnsureState(ConnectionLifecycleState.Recovering);
        RetryMetadata retry = Retry ?? throw new InvalidOperationException("Retry metadata is required for recovery.");
        return this with
        {
            State = ConnectionLifecycleState.RetryWaiting,
            Retry = retry.Next(delay, retryNotBefore)
        };
    }

    public ConnectionSessionState RetrySelecting()
    {
        EnsureState(ConnectionLifecycleState.RetryWaiting);
        return this with
        {
            State = ConnectionLifecycleState.Recovering,
            Candidate = null,
            Failure = null
        };
    }

    public ConnectionSessionState Fail(ConnectionFailure failure)
    {
        EnsureNotTerminal();
        ArgumentNullException.ThrowIfNull(failure);
        return this with { State = ConnectionLifecycleState.Failed, Failure = failure };
    }

    public ConnectionSessionState Cancel()
    {
        EnsureNotTerminal();
        return this with { State = ConnectionLifecycleState.Cancelled };
    }

    public ConnectionSessionState Complete()
    {
        EnsureState(ConnectionLifecycleState.Connected, ConnectionLifecycleState.Transferring);
        return this with { State = ConnectionLifecycleState.Completed, Failure = null };
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
            throw new InvalidOperationException($"State {State} is terminal.");
    }

    private void EnsureState(params ConnectionLifecycleState[] allowed)
    {
        if (!allowed.Contains(State))
            throw new InvalidOperationException($"Cannot transition from {State}.");
    }
}
