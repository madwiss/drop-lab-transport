namespace Drop.Routing;

public sealed class RouteFallbackHandler
{
    public IReadOnlyList<ConnectionCandidate> GetFallbackCandidates(
        PeerRoutes peer,
        ConnectionCandidate failedCandidate,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(failedCandidate);

        return RouteSelector.OrderEligible(peer, requiredCapabilities)
            .Where(candidate => candidate.CandidateId != failedCandidate.CandidateId)
            .ToArray();
    }
}
