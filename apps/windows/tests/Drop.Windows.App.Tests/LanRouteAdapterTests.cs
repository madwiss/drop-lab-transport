using System.Net;
using Drop.Discovery;
using Drop.Routing;
using Drop.Transport;

namespace Drop.Windows.App.Tests;

[TestClass]
public sealed class LanRouteAdapterTests
{
    [TestMethod]
    public void DiscoveryDeviceBecomesNeutralReachableLanRouteBeforeEndpointCreation()
    {
        DiscoveredDevice device = Device();

        PeerRoutes routes = LanRouteAdapter.ToPeerRoutes(device);

        Assert.AreEqual(device.DeviceId.ToString("D"), routes.PeerId);
        Assert.HasCount(1, routes.Candidates);
        ConnectionCandidate candidate = routes.Candidates[0];
        Assert.AreEqual(RouteKind.LocalLan, candidate.RouteKind);
        Assert.AreEqual(TransportKind.ReliableByteStream, candidate.TransportKind);
        Assert.AreEqual(CandidateAvailability.Reachable, candidate.Availability);
        Assert.IsTrue(candidate.Capabilities.HasFlag(RouteCapabilities.SupportsLargeTransfers));
    }

    [TestMethod]
    public void SelectedLanRouteUsesTransportFactoryForEndpointAndConnector()
    {
        DiscoveredDevice device = Device();
        RecordingFactory factory = new();

        SelectedTransportRoute selected = LanRouteAdapter.Select(device, factory);

        Assert.AreEqual(selected.Candidate, factory.EndpointCandidate);
        Assert.AreEqual(selected.Candidate, factory.ConnectorCandidate);
        Assert.AreSame(factory.Endpoint, selected.Endpoint);
        Assert.AreSame(factory.Connector, selected.Connector);
    }

    private static DiscoveredDevice Device() => new(
        Guid.NewGuid(),
        "Peer",
        "windows",
        1,
        "0.1.0",
        IPAddress.Parse("192.168.1.25"),
        50444);

    private sealed class RecordingFactory : IRouteTransportFactory
    {
        public StubEndpoint Endpoint { get; } = new();
        public StubConnector Connector { get; } = new();
        public ConnectionCandidate? EndpointCandidate { get; private set; }
        public ConnectionCandidate? ConnectorCandidate { get; private set; }

        public ITransportConnector CreateConnector(ConnectionCandidate candidate)
        {
            ConnectorCandidate = candidate;
            return Connector;
        }

        public ITransportEndpoint CreateEndpoint(ConnectionCandidate candidate, DiscoveredDevice device)
        {
            EndpointCandidate = candidate;
            return Endpoint;
        }

        public StartedTransportListener CreateStartedListener() => throw new NotSupportedException();
    }

    private sealed class StubEndpoint : ITransportEndpoint;

    private sealed class StubConnector : ITransportConnector
    {
        public ValueTask<IReliableByteStream> ConnectAsync(
            ITransportEndpoint endpoint,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
