using System.Text.Json;

namespace Drop.Security;

public sealed class JsonTrustedPeerStore : ITrustedPeerStore
{
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public JsonTrustedPeerStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
    }

    public async ValueTask<IReadOnlyList<TrustedPeer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await LoadAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    public async ValueTask<TrustedPeer?> FindAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        var peers = await GetAllAsync(cancellationToken);
        return peers.FirstOrDefault(peer => peer.DeviceId == deviceId);
    }

    public async ValueTask AddOrUpdateAsync(TrustedPeer peer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peer);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var peers = (await LoadAsync(cancellationToken)).ToList();
            peers.RemoveAll(item => item.DeviceId == peer.DeviceId);
            peers.Add(peer);
            await SaveAsync(peers, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var peers = (await LoadAsync(cancellationToken)).ToList();
            bool removed = peers.RemoveAll(peer => peer.DeviceId == deviceId) > 0;
            if (removed) await SaveAsync(peers, cancellationToken);
            return removed;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> IsTrustedAsync(string deviceId, string fingerprint, CancellationToken cancellationToken = default)
    {
        var peer = await FindAsync(deviceId, cancellationToken);
        return peer is { IsTrusted: true } && peer.Fingerprint == fingerprint;
    }

    private async Task<List<TrustedPeer>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<TrustedPeer>>(stream, Options, cancellationToken) ?? [];
    }

    private async Task SaveAsync(List<TrustedPeer> peers, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, peers, Options, cancellationToken);
    }
}
