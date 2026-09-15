using Drop.Protocol;

namespace Drop.Routing.Tests;

[TestClass]
public sealed class ConnectionStateTests
{
    private static readonly ConnectionCandidate Candidate = new(
        "lan-1",
        RouteKind.LocalLan,
        TransportKind.ReliableByteStream,
        RouteCapabilities.Reliable | RouteCapabilities.Ordered | RouteCapabilities.Bidirectional,
        CandidateAvailability.Reachable);

    [TestMethod]
    public void RetryFlowTransitionsThroughRecoveringAndRetryWaiting()
    {
        RetryMetadata retry = new(Attempt: 0, MaxAttempts: 3, Delay: TimeSpan.Zero);
        ConnectionFailure failure = new(
            ConnectionFailureKind.ConnectionTimeout,
            IsRetryable: true,
            "Timed out connecting to candidate.");

        ConnectionSessionState recovering = ConnectionSessionState.Selecting()
            .Connecting(Candidate)
            .Recover(failure, retry);
        ConnectionSessionState waiting = recovering.WaitForRetry(TimeSpan.FromSeconds(2));
        ConnectionSessionState selectingAgain = waiting.RetrySelecting();

        Assert.AreEqual(ConnectionLifecycleState.Recovering, recovering.State);
        Assert.AreEqual(ConnectionLifecycleState.RetryWaiting, waiting.State);
        Assert.AreEqual(1, waiting.Retry!.Attempt);
        Assert.AreEqual(TimeSpan.FromSeconds(2), waiting.Retry.Delay);
        Assert.AreEqual(ConnectionLifecycleState.Recovering, selectingAgain.State);
        Assert.IsNull(selectingAgain.Candidate);
        Assert.IsNull(selectingAgain.Failure);
    }

    [TestMethod]
    public void FailedStateIsTerminalAndRetainsTypedFailure()
    {
        ConnectionFailure failure = new(
            ConnectionFailureKind.RetryExhausted,
            IsRetryable: false);

        ConnectionSessionState failed = ConnectionSessionState.Selecting().Fail(failure);

        Assert.AreEqual(ConnectionLifecycleState.Failed, failed.State);
        Assert.IsTrue(failed.IsTerminal);
        Assert.AreEqual(ConnectionFailureKind.RetryExhausted, failed.Failure!.Kind);
        Assert.Throws<InvalidOperationException>(() => failed.Cancel());
    }

    [TestMethod]
    public void ConnectedTransferCanComplete()
    {
        ConnectionSessionState completed = ConnectionSessionState.Selecting()
            .Connecting(Candidate)
            .Connected()
            .Transferring()
            .Complete();

        Assert.AreEqual(ConnectionLifecycleState.Completed, completed.State);
        Assert.IsTrue(completed.IsTerminal);
    }

    [TestMethod]
    public void NonRetryableFailureCannotEnterRecovery()
    {
        ConnectionSessionState connecting = ConnectionSessionState.Selecting().Connecting(Candidate);
        ConnectionFailure failure = new(ConnectionFailureKind.AuthenticationFailed, IsRetryable: false);
        RetryMetadata retry = new(0, 3, TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() => connecting.Recover(failure, retry));
    }

    [TestMethod]
    public void RetryMetadataRejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryMetadata(-1, 3, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryMetadata(0, -1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryMetadata(4, 3, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryMetadata(0, 3, TimeSpan.FromMilliseconds(-1)));
    }

    [TestMethod]
    [DataRow(TransferFailureKind.Timeout, ConnectionFailureKind.ConnectionTimeout, true)]
    [DataRow(TransferFailureKind.Cancelled, ConnectionFailureKind.Cancelled, false)]
    [DataRow(TransferFailureKind.Transport, ConnectionFailureKind.TransportInterrupted, true)]
    [DataRow(TransferFailureKind.Protocol, ConnectionFailureKind.ProtocolError, false)]
    public void TransferFailuresMapToTypedConnectionFailures(
        TransferFailureKind transferKind,
        ConnectionFailureKind expectedKind,
        bool retryable)
    {
        TransferFailedException transferFailure = new(transferKind, "failure");

        ConnectionFailure mapped = ConnectionFailureMapper.FromTransferFailure(transferFailure);

        Assert.AreEqual(expectedKind, mapped.Kind);
        Assert.AreEqual(retryable, mapped.IsRetryable);
    }
}
