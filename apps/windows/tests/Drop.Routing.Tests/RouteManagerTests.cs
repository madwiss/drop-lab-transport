namespace Drop.Routing.Tests;

[TestClass]
public sealed class RouteManagerTests
{
    private const RouteCapabilities Reliable = RouteCapabilities.Reliable | RouteCapabilities.Ordered | RouteCapabilities.Bidirectional;

    [TestMethod]
    public void SelectRouteReturnsHighestPriorityCompatibleCandidate()
    {
        RouteManager manager = new();
        PeerRoutes peer = new("peer", [
            Candidate("low", RouteKind.LocalLan, 1),
            Candidate("high", RouteKind.LocalLan, 10)
        ]);

        Assert.AreEqual("high", manager.SelectRoute(peer, Reliable)?.CandidateId);
    }

    [TestMethod]
    public void FallbackSkipsFailedCandidate()
    {
        RouteManager manager = new();
        PeerRoutes peer = new("peer", [Candidate("first", RouteKind.LocalLan), Candidate("second", RouteKind.RemoteDirect)]);

        Assert.AreEqual("second", manager.GetFallbackRoutes(peer, peer.Candidates[0], Reliable)[0].CandidateId);
    }

    [TestMethod]
    public void IncompatibleRoutesAreRejected()
    {
        RouteManager manager = new();
        PeerRoutes peer = new("peer", [new ConnectionCandidate("bad", RouteKind.LocalLan, TransportKind.Datagram, RouteCapabilities.None, CandidateAvailability.Reachable)]);

        Assert.IsNull(manager.SelectRoute(peer, Reliable));
    }

    private static ConnectionCandidate Candidate(string id, RouteKind kind, int priority = 0) =>
        new(id, kind, TransportKind.ReliableByteStream, Reliable | RouteCapabilities.SupportsLargeTransfers, CandidateAvailability.Reachable, priority);
}
