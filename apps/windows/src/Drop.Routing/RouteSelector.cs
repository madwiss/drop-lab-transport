namespace Drop.Routing;

public static class RouteSelector
{
    public static IReadOnlyList<ConnectionCandidate> OrderEligible(
        PeerRoutes peer,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(peer);

        return peer.Candidates
            .Where(candidate => candidate.IsEligible(requiredCapabilities))
            .OrderBy(candidate => RouteRank(candidate.RouteKind))
            .ThenByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    public static ConnectionCandidate? SelectPreferred(
        PeerRoutes peer,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None) =>
        OrderEligible(peer, requiredCapabilities).FirstOrDefault();

    private static int RouteRank(RouteKind kind) => kind switch
    {
        RouteKind.LocalLan => 0,
        RouteKind.LocalPeerToPeer => 1,
        RouteKind.RemoteDirect => 2,
        RouteKind.RemoteRelay => 3,
        _ => int.MaxValue
    };
}
