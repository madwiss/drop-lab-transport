using System.Net;
using Drop.Discovery;
using Drop.Routing;
using Drop.Transport;

namespace Drop.Windows.App.Tests;

[TestClass]
public sealed class TransportRouteDiscoveryTests
{
    [TestMethod]
    public void DiscoveryDeviceProducesTransportCandidate()
    {
        DiscoveredDevice device = Device();

        TransportRegistry registry = DefaultTransportRegistry.Create();
        TransportCandidateResolver resolver = new(registry);
        TransportRouteDiscoveryService discovery = new(resolver);

        PeerRoutes routes = discovery.DiscoverRoutes(device);

        Assert.AreEqual(device.DeviceId.ToString("D"), routes.PeerId);
        Assert.HasCount(1, routes.Candidates);

        ConnectionCandidate candidate = routes.Candidates[0];

        Assert.AreEqual(RouteKind.LocalLan, candidate.RouteKind);
        Assert.AreEqual(TransportKind.ReliableByteStream, candidate.TransportKind);
        Assert.AreEqual(CandidateAvailability.Reachable, candidate.Availability);
        Assert.IsTrue(candidate.Capabilities.HasFlag(RouteCapabilities.SupportsLargeTransfers));
    }

    [TestMethod]
    public void SelectPreferredUsesDiscoveredEndpointWithoutRecreatingIt()
    {
        DiscoveredDevice device = Device();

        TransportRegistry registry = DefaultTransportRegistry.Create();
        TransportCandidateResolver resolver = new(registry);
        TransportRouteDiscoveryService discovery = new(resolver);

        ThrowingEndpointFactory factory = new();

        SelectedTransportRoute selected =
            discovery.SelectPreferred(device, factory);

        Assert.AreEqual(device.Port, selected.Endpoint.DiscoveryPort);
    }

    private sealed class ThrowingEndpointFactory : IRouteTransportFactory
    {
        public ITransportConnector CreateConnector(
            ConnectionCandidate candidate)
        {
            return new ThrowingConnector();
        }

        public ITransportEndpoint CreateEndpoint(
            ConnectionCandidate candidate,
            DiscoveredDevice device)
        {
            throw new InvalidOperationException(
                "CreateEndpoint must not be called for a discovered transport candidate.");
        }

        public StartedTransportListener CreateStartedListener()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingConnector : ITransportConnector
    {
        public ValueTask<IReliableByteStream> ConnectAsync(
            ITransportEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
    private static DiscoveredDevice Device() => new(
        Guid.NewGuid(),
        "Peer",
        "windows",
        1,
        "0.1.0",
        IPAddress.Parse("192.168.1.25"),
        50444);
}

