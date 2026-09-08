using System.Net;

namespace Drop.Discovery.Tests;

[TestClass]
public sealed class DiscoveryRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Upsert_DeduplicatesDeviceIdAcrossSourcesAndAddresses()
    {
        var localId = Guid.NewGuid();
        var peerId = Guid.NewGuid();
        var registry = new DiscoveryRegistry(localId);

        var appeared = registry.Upsert(Record("peer-a", peerId, "Peer", "10.0.0.20", "fe80::20"));
        var duplicate = registry.Upsert(Record("peer-b", peerId, "Peer", "10.0.0.21"));

        Assert.AreEqual(DiscoveryChangeKind.Appeared, appeared.Single().Kind);
        Assert.IsEmpty(duplicate);
        var device = registry.Devices.Single();
        Assert.AreEqual(peerId, device.DeviceId);
        Assert.AreEqual(IPAddress.Parse("10.0.0.20"), device.Address);
    }

    [TestMethod]
    public void Upsert_IgnoresLocalDeviceId()
    {
        var localId = Guid.NewGuid();
        var registry = new DiscoveryRegistry(localId);

        Assert.IsEmpty(registry.Upsert(Record("self", localId, "This PC", "192.168.1.5")));
        Assert.IsEmpty(registry.Devices);
    }

    [TestMethod]
    public void Upsert_EmitsUpdateWhenVisibleRecordChanges()
    {
        var peerId = Guid.NewGuid();
        var registry = new DiscoveryRegistry(Guid.NewGuid());
        registry.Upsert(Record("peer", peerId, "Old name", "192.168.1.20"));

        var changes = registry.Upsert(Record("peer", peerId, "New name", "192.168.1.21", port: 5051));

        var change = changes.Single();
        Assert.AreEqual(DiscoveryChangeKind.Updated, change.Kind);
        Assert.AreEqual("New name", change.Device.DeviceName);
        Assert.AreEqual(5051, change.Device.Port);
        Assert.AreEqual(IPAddress.Parse("192.168.1.21"), change.Device.Address);
    }

    [TestMethod]
    public void RemoveSource_KeepsDeviceUntilItsLastSourceDisappears()
    {
        var peerId = Guid.NewGuid();
        var registry = new DiscoveryRegistry(Guid.NewGuid());
        registry.Upsert(Record("peer-a", peerId, "Peer", "192.168.1.20", expires: Now.AddMinutes(2)));
        registry.Upsert(Record("peer-b", peerId, "Peer", "192.168.1.21", expires: Now.AddMinutes(1)));

        Assert.IsEmpty(registry.RemoveSource("peer-b"));
        var removed = registry.RemoveSource("peer-a");

        Assert.AreEqual(DiscoveryChangeKind.Disappeared, removed.Single().Kind);
        Assert.IsEmpty(registry.Devices);
    }

    [TestMethod]
    public void RemoveExpired_KeepsLiveSourceThenRemovesDevice()
    {
        var peerId = Guid.NewGuid();
        var registry = new DiscoveryRegistry(Guid.NewGuid());
        registry.Upsert(Record("older", peerId, "Old", "192.168.1.20", expires: Now.AddMinutes(3)));
        registry.Upsert(Record("newer", peerId, "New", "192.168.1.21", expires: Now.AddMinutes(1)));

        var firstExpiry = registry.RemoveExpired(Now.AddMinutes(2));
        Assert.IsEmpty(firstExpiry);
        Assert.AreEqual("Old", registry.Devices.Single().DeviceName);

        var removed = registry.RemoveExpired(Now.AddMinutes(4));
        Assert.AreEqual(DiscoveryChangeKind.Disappeared, removed.Single().Kind);
    }

    private static DnsSdRecord Record(
        string source,
        Guid deviceId,
        string name,
        string address,
        string? secondAddress = null,
        int port = 5050,
        DateTimeOffset? expires = null)
    {
        var addresses = secondAddress is null
            ? new[] { IPAddress.Parse(address) }
            : new[] { IPAddress.Parse(address), IPAddress.Parse(secondAddress) };
        return new(source, deviceId, name, "windows", 1, "0.1.0", port, addresses, expires ?? Now.AddMinutes(1));
    }
}
