namespace Drop.Discovery;

public sealed class LanDiscoveryService : IDropDiscoveryService
{
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromSeconds(15);
    private readonly Func<IDnsSdBackend> _backendFactory;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private DiscoveryRegistry? _registry;
    private IDnsSdBackend? _backend;
    private CancellationTokenSource? _maintenanceCancellation;
    private CancellationTokenRegistration _callerCancellation;
    private Task? _maintenanceTask;
    private volatile bool _isRunning;
    private bool _disposed;

    public LanDiscoveryService() : this(static () => new MakaretuDnsSdBackend())
    {
    }

    internal LanDiscoveryService(Func<IDnsSdBackend> backendFactory)
    {
        _backendFactory = backendFactory;
    }

    public event EventHandler<DiscoveredDeviceEventArgs>? DeviceAppeared;
    public event EventHandler<DiscoveredDeviceEventArgs>? DeviceUpdated;
    public event EventHandler<DiscoveredDeviceEventArgs>? DeviceDisappeared;

    public bool IsRunning => _isRunning;
    public IReadOnlyCollection<DiscoveredDevice> Devices => _registry?.Devices ?? [];

    public async Task StartAsync(DropAdvertisement advertisement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(advertisement);
        advertisement.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isRunning) throw new InvalidOperationException("Discovery is already running.");

            var backend = _backendFactory();
            var registry = new DiscoveryRegistry(advertisement.DeviceId);
            _registry = registry;
            backend.RecordReceived += OnRecordReceived;
            backend.RecordRemoved += OnRecordRemoved;
            try
            {
                await backend.StartAsync(advertisement, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                backend.RecordReceived -= OnRecordReceived;
                backend.RecordRemoved -= OnRecordRemoved;
                try { await backend.StopAsync(CancellationToken.None).ConfigureAwait(false); }
                finally { await backend.DisposeAsync().ConfigureAwait(false); }
                _registry = null;
                throw;
            }

            _backend = backend;
            _maintenanceCancellation = new CancellationTokenSource();
            _maintenanceTask = MaintainAsync(backend, _maintenanceCancellation.Token);
            _callerCancellation = cancellationToken.Register(
                static state => _ = ((LanDiscoveryService)state!).StopAsync(), this);
            _isRunning = true;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isRunning && _backend is null) return;
            _isRunning = false;
            await _callerCancellation.DisposeAsync().ConfigureAwait(false);

            var maintenanceCancellation = _maintenanceCancellation;
            var maintenanceTask = _maintenanceTask;
            var backend = _backend;
            _maintenanceCancellation = null;
            _maintenanceTask = null;
            _backend = null;

            maintenanceCancellation?.Cancel();
            if (maintenanceTask is not null)
            {
                try { await maintenanceTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            maintenanceCancellation?.Dispose();

            if (backend is not null)
            {
                backend.RecordReceived -= OnRecordReceived;
                backend.RecordRemoved -= OnRecordRemoved;
                try
                {
                    await backend.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await backend.DisposeAsync().ConfigureAwait(false);
                }
            }

            _registry?.Clear();
            _registry = null;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _lifecycle.Dispose();
    }

    private async Task MaintainAsync(IDnsSdBackend backend, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(MaintenanceInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            backend.Query();
            Dispatch(_registry?.RemoveExpired(DateTimeOffset.UtcNow) ?? []);
        }
    }

    private void OnRecordReceived(object? sender, DnsSdRecord record) =>
        Dispatch(_registry?.Upsert(record) ?? []);

    private void OnRecordRemoved(object? sender, string sourceKey) =>
        Dispatch(_registry?.RemoveSource(sourceKey) ?? []);

    private void Dispatch(IEnumerable<DiscoveryChange> changes)
    {
        foreach (var change in changes)
        {
            var handler = change.Kind switch
            {
                DiscoveryChangeKind.Appeared => DeviceAppeared,
                DiscoveryChangeKind.Updated => DeviceUpdated,
                DiscoveryChangeKind.Disappeared => DeviceDisappeared,
                _ => null
            };
            handler?.Invoke(this, new DiscoveredDeviceEventArgs(change.Device));
        }
    }
}
