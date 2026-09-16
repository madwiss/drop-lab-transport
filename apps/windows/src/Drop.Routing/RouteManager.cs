namespace Drop.Routing;

public sealed class RouteManager
{
    private readonly RouteFallbackHandler fallbackHandler;

    public RouteManager(RouteFallbackHandler? fallbackHandler = null)
    {
        this.fallbackHandler = fallbackHandler ?? new RouteFallbackHandler();
    }

    public ConnectionCandidate? SelectRoute(
        PeerRoutes peer,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(peer);
        return RouteSelector.SelectPreferred(peer, requiredCapabilities);
    }

    public IReadOnlyList<ConnectionCandidate> EvaluateRoutes(
        PeerRoutes peer,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(peer);
        return RouteSelector.OrderEligible(peer, requiredCapabilities);
    }

    public IReadOnlyList<ConnectionCandidate> GetFallbackRoutes(
        PeerRoutes peer,
        ConnectionCandidate failedCandidate,
        RouteCapabilities requiredCapabilities = RouteCapabilities.None) =>
        fallbackHandler.GetFallbackCandidates(peer, failedCandidate, requiredCapabilities);
}
