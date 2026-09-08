using System.Net;

namespace Drop.Discovery.Tests;

[TestClass]
public sealed class LanDiscoveryServiceTests
{
    [TestMethod]
    public async Task StartStop_CanRestartAndReleasesEveryBackend()
    {
        var backends = new List<FakeDnsSdBackend>();
        await using var service = new LanDiscoveryService(() =>
        {
            var backend = new FakeDnsSdBackend();
            backends.Add(backend);
            return backend;
        });
        var advertisement = Advertisement();

        await service.StartAsync(advertisement);
        Assert.IsTrue(service.IsRunning);
        await service.StopAsync();
        Assert.IsFalse(service.IsRunning);
        await service.StartAsync(advertisement);
        await service.StopAsync();

        Assert.HasCount(2, backends);
        Assert.IsTrue(backends.All(value => value.StartCount == 1 && value.StopCount == 1 && value.DisposeCount == 1));
    }

    [TestMethod]
    public async Task Cancellation_StopsAndDisposesBackend()
    {
        var backend = new FakeDnsSdBackend();
        await using var service = new LanDiscoveryService(() => backend);
        using var cancellation = new CancellationTokenSource();
        await service.StartAsync(Advertisement(), cancellation.Token);

        cancellation.Cancel();
        await WaitUntilAsync(() => backend.DisposeCount == 1);

        Assert.IsFalse(service.IsRunning);
        Assert.AreEqual(1, backend.StopCount);
    }

    [TestMethod]
    public async Task BackendRecords_ProduceAppearedUpdatedAndDisappearedEvents()
    {
        var backend = new FakeDnsSdBackend();
        var local = Guid.NewGuid();
        var remote = Guid.NewGuid();
        await using var service = new LanDiscoveryService(() => backend);
        var events = new List<string>();
        service.DeviceAppeared += (_, e) => events.Add("appeared:" + e.Device.DeviceName);
        service.DeviceUpdated += (_, e) => events.Add("updated:" + e.Device.DeviceName);
        service.DeviceDisappeared += (_, e) => events.Add("disappeared:" + e.Device.DeviceName);
        await service.StartAsync(Advertisement(local));

        backend.Emit(Record("peer", remote, "Old"));
        backend.Emit(Record("peer", remote, "New"));
        backend.Remove("peer");

        CollectionAssert.AreEqual(new[] { "appeared:Old", "updated:New", "disappeared:New" }, events);
    }

    private static DropAdvertisement Advertisement(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Local PC", "windows", 1, "0.1.0", 5050);

    private static DnsSdRecord Record(string source, Guid id, string name) =>
        new(source, id, name, "windows", 1, "0.1.0", 5050,
            [IPAddress.Parse("192.168.1.20")], DateTimeOffset.UtcNow.AddMinutes(1));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout) await Task.Delay(10);
        Assert.IsTrue(condition(), "Condition was not reached before the timeout.");
    }

    private sealed class FakeDnsSdBackend : IDnsSdBackend
    {
        public event EventHandler<DnsSdRecord>? RecordReceived;
        public event EventHandler<string>? RecordRemoved;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }

        public Task StartAsync(DropAdvertisement advertisement, CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public void Query() { }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void Emit(DnsSdRecord record) => RecordReceived?.Invoke(this, record);
        public void Remove(string source) => RecordRemoved?.Invoke(this, source);
    }
}
