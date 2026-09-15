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

public sealed record ConnectionCandidate
{
    private const RouteCapabilities KnownCapabilities =
        RouteCapabilities.Reliable |
        RouteCapabilities.Ordered |
        RouteCapabilities.Bidirectional |
        RouteCapabilities.Encrypted |
        RouteCapabilities.AuthenticatedPeer |
        RouteCapabilities.SupportsLargeTransfers;

    public ConnectionCandidate(
        string candidateId,
        RouteKind routeKind,
        TransportKind transportKind,
        RouteCapabilities capabilities,
        CandidateAvailability availability,
        int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);

        if (!Enum.IsDefined(routeKind))
            throw new ArgumentOutOfRangeException(nameof(routeKind));
        if (!Enum.IsDefined(transportKind))
            throw new ArgumentOutOfRangeException(nameof(transportKind));
        if (!Enum.IsDefined(availability))
            throw new ArgumentOutOfRangeException(nameof(availability));
        if ((capabilities & ~KnownCapabilities) != 0)
            throw new ArgumentOutOfRangeException(nameof(capabilities));
        if (priority < 0)
            throw new ArgumentOutOfRangeException(nameof(priority));

        CandidateId = candidateId;
        RouteKind = routeKind;
        TransportKind = transportKind;
        Capabilities = capabilities;
        Availability = availability;
        Priority = priority;
    }

    public string CandidateId { get; }

    public RouteKind RouteKind { get; }

    public TransportKind TransportKind { get; }

    public RouteCapabilities Capabilities { get; }

    public CandidateAvailability Availability { get; }

    public int Priority { get; }

    public bool IsEligible(RouteCapabilities requiredCapabilities = RouteCapabilities.None) =>
        Availability == CandidateAvailability.Reachable &&
        TransportKind == TransportKind.ReliableByteStream &&
        (Capabilities & requiredCapabilities) == requiredCapabilities;
}

public sealed record PeerRoutes
{
    public PeerRoutes(string peerId, IEnumerable<ConnectionCandidate> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(candidates);

        PeerId = peerId;
        Candidates = Array.AsReadOnly(candidates.ToArray());
    }

    public string PeerId { get; }

    public IReadOnlyList<ConnectionCandidate> Candidates { get; }
}
