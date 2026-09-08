namespace Drop.Discovery;

public interface IDropDiscoveryService : IAsyncDisposable
{
    event EventHandler<DiscoveredDeviceEventArgs>? DeviceAppeared;
    event EventHandler<DiscoveredDeviceEventArgs>? DeviceUpdated;
    event EventHandler<DiscoveredDeviceEventArgs>? DeviceDisappeared;

    bool IsRunning { get; }
    IReadOnlyCollection<DiscoveredDevice> Devices { get; }

    Task StartAsync(DropAdvertisement advertisement, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
