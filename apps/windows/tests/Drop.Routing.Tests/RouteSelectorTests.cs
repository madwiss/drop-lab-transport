namespace Drop.Routing.Tests;

[TestClass]
public sealed class RouteSelectorTests
{
    private const RouteCapabilities ReliableStream =
        RouteCapabilities.Reliable | RouteCapabilities.Ordered | RouteCapabilities.Bidirectional;

    [TestMethod]
    public void SelectPreferredPrefersReachableLanOverRemoteCandidates()
    {
        PeerRoutes peer = new("peer-1",
        [
            Candidate("relay", RouteKind.RemoteRelay, priority: 100),
            Candidate("remote", RouteKind.RemoteDirect, priority: 100),
            Candidate("lan", RouteKind.LocalLan, priority: 0)
        ]);

        ConnectionCandidate? selected = RouteSelector.SelectPreferred(peer, ReliableStream);

        Assert.IsNotNull(selected);
        Assert.AreEqual("lan", selected.CandidateId);
    }

    [TestMethod]
    public void OrderEligibleUsesDeterministicFallbackOrdering()
    {
        PeerRoutes peer = new("peer-1",
        [
            Candidate("relay-b", RouteKind.RemoteRelay, priority: 5),
            Candidate("direct", RouteKind.RemoteDirect),
            Candidate("p2p", RouteKind.LocalPeerToPeer),
            Candidate("relay-a", RouteKind.RemoteRelay, priority: 5),
            Candidate("relay-high", RouteKind.RemoteRelay, priority: 10)
        ]);

        string[] ordered = RouteSelector.OrderEligible(peer, ReliableStream)
            .Select(candidate => candidate.CandidateId)
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "p2p", "direct", "relay-high", "relay-a", "relay-b" },
            ordered);
    }

    [TestMethod]
    public void SelectPreferredSkipsUnavailableAndUnsupportedCandidates()
    {
        ConnectionCandidate unavailableLan = Candidate(
            "lan",
            RouteKind.LocalLan,
            availability: CandidateAvailability.Unavailable);
        ConnectionCandidate unsupportedP2p = new(
            "p2p",
            RouteKind.LocalPeerToPeer,
            TransportKind.Datagram,
            RouteCapabilities.None,
            CandidateAvailability.Reachable);
        ConnectionCandidate remote = Candidate("remote", RouteKind.RemoteDirect);
        PeerRoutes peer = new("peer-1", [unavailableLan, unsupportedP2p, remote]);

        ConnectionCandidate? selected = RouteSelector.SelectPreferred(peer, ReliableStream);

        Assert.IsNotNull(selected);
        Assert.AreEqual("remote", selected.CandidateId);
    }

    [TestMethod]
    public void PeerRoutesSnapshotsCandidateCollection()
    {
        List<ConnectionCandidate> candidates =
        [
            Candidate("lan", RouteKind.LocalLan)
        ];
        PeerRoutes peer = new("peer-1", candidates);

        candidates.Clear();
        candidates.Add(Candidate("relay", RouteKind.RemoteRelay));

        Assert.AreEqual(1, peer.Candidates.Count);
        Assert.AreEqual("lan", peer.Candidates[0].CandidateId);
    }

    [TestMethod]
    public void ConnectionCandidateRejectsInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new ConnectionCandidate(
            " ", RouteKind.LocalLan, TransportKind.ReliableByteStream,
            ReliableStream, CandidateAvailability.Reachable));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionCandidate(
            "lan", (RouteKind)999, TransportKind.ReliableByteStream,
            ReliableStream, CandidateAvailability.Reachable));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConnectionCandidate(
            "lan", RouteKind.LocalLan, TransportKind.ReliableByteStream,
            ReliableStream, CandidateAvailability.Reachable, priority: -1));
    }

    private static ConnectionCandidate Candidate(
        string id,
        RouteKind routeKind,
        int priority = 0,
        CandidateAvailability availability = CandidateAvailability.Reachable) =>
        new(
            id,
            routeKind,
            TransportKind.ReliableByteStream,
            ReliableStream | RouteCapabilities.SupportsLargeTransfers,
            availability,
            priority);
}
