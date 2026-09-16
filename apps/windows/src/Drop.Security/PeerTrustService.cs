namespace Drop.Security;

public sealed class PeerTrustService
{
    private readonly ITrustedPeerStore store;

    public PeerTrustService(ITrustedPeerStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ValueTask<bool> IsTrustedAsync(
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken = default) =>
        store.IsTrustedAsync(deviceId, fingerprint, cancellationToken);

    public ValueTask TrustAfterAcceptedAsync(
        string deviceId,
        string fingerprint,
        string displayName,
        CancellationToken cancellationToken = default) =>
        store.AddOrUpdateAsync(new TrustedPeer(
            deviceId,
            fingerprint,
            displayName,
            true,
            DateTimeOffset.UtcNow), cancellationToken);
}
