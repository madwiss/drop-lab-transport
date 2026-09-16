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
        Assert.IsTrue(await reloaded.IsTrustedAsync("device-1", "fingerprint-1"));
        Assert.AreEqual("fingerprint-1", (await reloaded.FindAsync("device-1"))!.Fingerprint);
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
}
