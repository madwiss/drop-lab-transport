using Makaretu.Dns;

namespace Drop.Discovery;

internal sealed class MakaretuDnsSdBackend : IDnsSdBackend
{
    internal const string ServiceType = "_drop._tcp";
    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;

    public event EventHandler<DnsSdRecord>? RecordReceived;
    public event EventHandler<string>? RecordRemoved;

    public async Task StartAsync(DropAdvertisement advertisement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_discovery is not null) throw new InvalidOperationException("The mDNS backend is already running.");

        var profile = CreateProfile(advertisement);
        var discovery = new ServiceDiscovery { AnswersContainsAdditionalRecords = true };
        _discovery = discovery;
        _profile = profile;

        discovery.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
        discovery.ServiceInstanceShutdown += OnServiceInstanceShutdown;
        discovery.Advertise(profile);

        await Task.Run(() => discovery.Announce(profile, 2), CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        discovery.QueryServiceInstances(ServiceType);
    }

    public void Query() => _discovery?.QueryServiceInstances(ServiceType);

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var discovery = _discovery;
        if (discovery is null) return Task.CompletedTask;

        discovery.ServiceInstanceDiscovered -= OnServiceInstanceDiscovered;
        discovery.ServiceInstanceShutdown -= OnServiceInstanceShutdown;
        if (_profile is not null) discovery.Unadvertise(_profile);
        _profile = null;
        _discovery = null;
        discovery.Dispose();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _discovery?.Dispose();
        _discovery = null;
        _profile = null;
        return ValueTask.CompletedTask;
    }

    internal static ServiceProfile CreateProfile(DropAdvertisement advertisement)
    {
        advertisement.Validate();
        var profile = new ServiceProfile(
            advertisement.DeviceId.ToString("D"), ServiceType, checked((ushort)advertisement.Port));
        profile.AddProperty("deviceId", advertisement.DeviceId.ToString("D"));
        profile.AddProperty("deviceName", advertisement.DeviceName);
        profile.AddProperty("platform", advertisement.Platform);
        profile.AddProperty("protocolVersion", advertisement.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        profile.AddProperty("appVersion", advertisement.AppVersion);
        return profile;
    }

    private void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs args)
    {
        if (MdnsRecordParser.TryParse(args.ServiceInstanceName, args.Message, DateTimeOffset.UtcNow, out var record))
            RecordReceived?.Invoke(this, record);
    }

    private void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs args) =>
        RecordRemoved?.Invoke(this, args.ServiceInstanceName.ToString());
}
