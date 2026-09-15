namespace Drop.Routing;

public enum RouteKind
{
    LocalLan = 0,
    LocalPeerToPeer = 1,
    RemoteDirect = 2,
    RemoteRelay = 3
}

public enum TransportKind
{
    ReliableByteStream = 0,
    Datagram = 1
}

[Flags]
public enum RouteCapabilities
{
    None = 0,
    Reliable = 1 << 0,
    Ordered = 1 << 1,
    Bidirectional = 1 << 2,
    Encrypted = 1 << 3,
    AuthenticatedPeer = 1 << 4,
    SupportsLargeTransfers = 1 << 5
}

public enum CandidateAvailability
{
    Unknown = 0,
    Reachable = 1,
    Unavailable = 2
}

public sealed record ConnectionCandidate(
    string CandidateId,
    RouteKind RouteKind,
    TransportKind TransportKind,
    RouteCapabilities Capabilities,
    CandidateAvailability Availability,
    int Priority = 0)
{
    public bool IsEligible(RouteCapabilities requiredCapabilities = RouteCapabilities.None) =>
        Availability == CandidateAvailability.Reachable &&
        TransportKind == TransportKind.ReliableByteStream &&
        (Capabilities & requiredCapabilities) == requiredCapabilities;
}

public sealed record PeerRoutes(string PeerId, IReadOnlyList<ConnectionCandidate> Candidates)
{
    public PeerRoutes(string peerId, IEnumerable<ConnectionCandidate> candidates)
        : this(peerId, candidates?.ToArray() ?? throw new ArgumentNullException(nameof(candidates)))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
    }
}
