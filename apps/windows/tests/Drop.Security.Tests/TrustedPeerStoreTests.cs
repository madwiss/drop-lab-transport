namespace Drop.Security.Tests;

[TestClass]
public sealed class TrustedPeerStoreTests
{
    [TestMethod]
    public async Task TrustedPeerPersistsAndCanBeVerifiedAfterRecreate()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "trusted.json");
        var peer = new TrustedPeer("device-1", "fingerprint-1", "Laptop", true, DateTimeOffset.UtcNow);

        await new JsonTrustedPeerStore(file).AddOrUpdateAsync(peer);

        var reloaded = new JsonTrustedPeerStore(file);
        TrustedPeer? persisted = await reloaded.FindAsync("device-1");

        Assert.IsNotNull(persisted);
        Assert.AreEqual("device-1", persisted.DeviceId);
        Assert.AreEqual("fingerprint-1", persisted.Fingerprint);
        Assert.AreEqual("Laptop", persisted.DisplayName);
        Assert.IsTrue(persisted.IsTrusted);
        Assert.IsTrue(await reloaded.IsTrustedAsync("device-1", "fingerprint-1"));
    }

    [TestMethod]
    public async Task RemoveDeletesTrustedPeer()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "trusted.json");
        var store = new JsonTrustedPeerStore(file);
        await store.AddOrUpdateAsync(new TrustedPeer("device-1", "fingerprint-1", "Laptop", true, DateTimeOffset.UtcNow));

        Assert.IsTrue(await store.RemoveAsync("device-1"));
        Assert.IsFalse(await store.IsTrustedAsync("device-1", "fingerprint-1"));
    }

    [TestMethod]
    public async Task UnknownPeerIsNotTrusted()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "trusted.json");
        var store = new JsonTrustedPeerStore(file);

        Assert.IsFalse(await store.IsTrustedAsync("unknown-device", "unknown-fingerprint"));
        Assert.IsNull(await store.FindAsync("unknown-device"));
    }

    [TestMethod]
    public async Task AcceptedTransferTransitionsPeerFromUntrustedToTrustedWithIdentity()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "trusted.json");
        var store = new JsonTrustedPeerStore(file);
        var service = new PeerTrustService(store);
        await store.AddOrUpdateAsync(new TrustedPeer(
            "device-1",
            "fingerprint-1",
            "Laptop",
            false,
            DateTimeOffset.UtcNow));

        Assert.IsFalse(await service.IsTrustedAsync("device-1", "fingerprint-1"));

        await service.TrustAfterAcceptedAsync("device-1", "fingerprint-1", "Laptop");

        TrustedPeer? acceptedPeer = await store.FindAsync("device-1");
        Assert.IsNotNull(acceptedPeer);
        Assert.AreEqual("device-1", acceptedPeer.DeviceId);
        Assert.AreEqual("fingerprint-1", acceptedPeer.Fingerprint);
        Assert.IsTrue(acceptedPeer.IsTrusted);
        Assert.IsTrue(await service.IsTrustedAsync("device-1", "fingerprint-1"));
        Assert.IsFalse(await service.IsTrustedAsync("device-1", "different-fingerprint"));
    }

    [TestMethod]
    public async Task AcceptingAlreadyTrustedPeerKeepsSingleTrustedIdentityBinding()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "trusted.json");
        var store = new JsonTrustedPeerStore(file);
        var service = new PeerTrustService(store);
        await service.TrustAfterAcceptedAsync("device-1", "fingerprint-1", "Laptop");

        await service.TrustAfterAcceptedAsync("device-1", "fingerprint-1", "Renamed Laptop");

        IReadOnlyList<TrustedPeer> peers = await store.GetAllAsync();
        Assert.HasCount(1, peers);
        Assert.AreEqual("device-1", peers[0].DeviceId);
        Assert.AreEqual("fingerprint-1", peers[0].Fingerprint);
        Assert.AreEqual("Renamed Laptop", peers[0].DisplayName);
        Assert.IsTrue(peers[0].IsTrusted);
        Assert.IsTrue(await service.IsTrustedAsync("device-1", "fingerprint-1"));
    }
}
