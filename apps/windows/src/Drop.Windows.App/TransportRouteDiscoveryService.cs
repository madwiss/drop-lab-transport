using Drop.Discovery;
using Drop.Routing;
using Drop.Transport;

namespace Drop.Windows;

/// <summary>
/// Builds routing candidates by asking registered transports
/// what connection options are available.
/// </summary>
public sealed class TransportRouteDiscoveryService
{
    private readonly TransportCandidateResolver _candidateResolver;

    public TransportRouteDiscoveryService(
        TransportCandidateResolver candidateResolver)
    {
        ArgumentNullException.ThrowIfNull(candidateResolver);

        _candidateResolver = candidateResolver;
    }

    public PeerRoutes DiscoverRoutes(
        DiscoveredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        IReadOnlyCollection<TransportCandidate> transportCandidates =
            ResolveTransportCandidates(device);

        return BuildPeerRoutes(device, transportCandidates);
    }

    public SelectedTransportRoute SelectPreferred(
        DiscoveredDevice device,
        IRouteTransportFactory transportFactory)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(transportFactory);

        IReadOnlyCollection<TransportCandidate> transportCandidates =
            ResolveTransportCandidates(device);

        PeerRoutes routes =
            BuildPeerRoutes(device, transportCandidates);

        ConnectionCandidate candidate =
            RouteSelector.SelectPreferred(routes)
            ?? throw new InvalidOperationException(
                "No eligible route is available.");

        TransportCandidate transportCandidate =
            transportCandidates.Single(candidateOption =>
                candidateOption.CandidateId == candidate.CandidateId);

        return new SelectedTransportRoute(
            candidate,
            transportCandidate.Endpoint,
            transportFactory.CreateConnector(candidate));
    }

    private IReadOnlyCollection<TransportCandidate> ResolveTransportCandidates(
        DiscoveredDevice device)
    {
        TransportDiscoveryContext context =
            new(
                device.DeviceId.ToString("D"),
                device.Address.ToString(),
                device.Port);

        return _candidateResolver.Resolve(context);
    }

    private static PeerRoutes BuildPeerRoutes(
        DiscoveredDevice device,
        IReadOnlyCollection<TransportCandidate> transportCandidates)
    {
        ConnectionCandidate[] routes =
            transportCandidates
                .Select(TransportCandidateMapper.Map)
                .ToArray();

        return new PeerRoutes(
            device.DeviceId.ToString("D"),
            routes);
    }
}