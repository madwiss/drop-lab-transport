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

        TransportDiscoveryContext context =
            new(
                device.DeviceId.ToString("D"),
                device.Address.ToString(),
                device.Port);

        IReadOnlyCollection<TransportCandidate> transportCandidates =
            _candidateResolver.Resolve(context);

        ConnectionCandidate[] routes =
            transportCandidates
                .Select(TransportCandidateMapper.Map)
                .ToArray();

        return new PeerRoutes(
            device.DeviceId.ToString("D"),
            routes);
    }

    public SelectedTransportRoute SelectPreferred(
        DiscoveredDevice device,
        IRouteTransportFactory transportFactory)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(transportFactory);

        PeerRoutes routes = DiscoverRoutes(device);

        ConnectionCandidate candidate =
            RouteSelector.SelectPreferred(routes)
            ?? throw new InvalidOperationException(
                "No eligible route is available.");

        return new SelectedTransportRoute(
            candidate,
            transportFactory.CreateEndpoint(candidate, device),
            transportFactory.CreateConnector(candidate));
    }
}