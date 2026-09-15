using Drop.Security;

namespace Drop.Transport.Tests;

[TestClass]
public sealed class TransportSessionTests
{
    [TestMethod]
    public void LegacyLanSessionIsExplicitlyUnauthenticatedAndUsable()
    {
        DiscoveryPeerIdentity discovery = new(
            DeviceId: "mdns-device-id",
            HostName: "peer.local",
            EndpointLabel: "192.168.1.10:53317");

        TransportSessionSecurity session = TransportSessionSecurity.LegacyUnauthenticated(discovery);

        Assert.IsFalse(session.AuthenticationRequired);
        Assert.IsTrue(session.CanUseSession);
        Assert.AreEqual(PeerAuthenticationState.Unauthenticated, session.PeerIdentity.AuthenticationState);
        Assert.IsNull(session.PeerIdentity.AuthenticatedIdentity);
    }

    [TestMethod]
    public void AuthenticationRequiredSessionCannotProceedFromDiscoveryMetadataAlone()
    {
        PeerSessionIdentity peer = PeerSessionIdentity.Unauthenticated(
            new DiscoveryPeerIdentity(DeviceId: "known-device"));
        TransportSessionSecurity session = new(peer, AuthenticationRequired: true);

        Assert.IsFalse(session.CanUseSession);
        Assert.AreEqual(PeerAuthenticationState.Unauthenticated, peer.AuthenticationState);
    }
}
