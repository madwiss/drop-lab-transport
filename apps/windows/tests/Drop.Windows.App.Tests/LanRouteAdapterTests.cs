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

    private static DiscoveredDevice Device() => new(
        Guid.NewGuid(),
        "Peer",
        "windows",
        1,
        "0.1.0",
        IPAddress.Parse("192.168.1.25"),
        50444);
}

