namespace Drop.Security;

public interface ITrustedPeerStore
{
    ValueTask<IReadOnlyList<TrustedPeer>> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<TrustedPeer?> FindAsync(string deviceId, CancellationToken cancellationToken = default);

    ValueTask AddOrUpdateAsync(TrustedPeer peer, CancellationToken cancellationToken = default);

    ValueTask<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default);

    ValueTask<bool> IsTrustedAsync(string deviceId, string fingerprint, CancellationToken cancellationToken = default);
}
