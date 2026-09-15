using Drop.Security;

namespace Drop.Transport;

/// <summary>
/// Transport-neutral security context for an established byte-stream session.
/// Legacy Protocol v1 LAN connections use an explicitly unauthenticated context.
/// </summary>
public sealed record TransportSessionSecurity(PeerSessionIdentity PeerIdentity, bool AuthenticationRequired)
{
    public TransportSessionSecurity(PeerSessionIdentity peerIdentity)
        : this(peerIdentity, AuthenticationRequired: false)
    {
    }

    public static TransportSessionSecurity LegacyUnauthenticated(
        DiscoveryPeerIdentity? discoveryIdentity = null) =>
        new(PeerSessionIdentity.Unauthenticated(discoveryIdentity), AuthenticationRequired: false);

    public bool CanUseSession => !AuthenticationRequired ||
        PeerIdentity.AuthenticationState == PeerAuthenticationState.Authenticated;
}
