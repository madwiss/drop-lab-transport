using System.Net;

namespace Drop.Discovery;

internal sealed record DnsSdRecord(
    string SourceKey,
    Guid DeviceId,
    string DeviceName,
    string Platform,
    int ProtocolVersion,
    string AppVersion,
    int Port,
    IReadOnlyList<IPAddress> Addresses,
    DateTimeOffset ExpiresAtUtc);

internal enum DiscoveryChangeKind
{
    Appeared,
    Updated,
    Disappeared
}

internal sealed record DiscoveryChange(DiscoveryChangeKind Kind, DiscoveredDevice Device);

internal sealed class DiscoveryRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DnsSdRecord> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Guid _localDeviceId;
    private readonly IReadOnlyCollection<LocalUnicastAddress> _localAddresses;

    public DiscoveryRegistry(Guid localDeviceId)
        : this(localDeviceId, DiscoveryAddressSelector.GetLocalAddresses())
    {
    }

    internal DiscoveryRegistry(
        Guid localDeviceId,
        IReadOnlyCollection<LocalUnicastAddress> localAddresses)
    {
        _localDeviceId = localDeviceId;
        _localAddresses = localAddresses;
    }

    public IReadOnlyCollection<DiscoveredDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return ProjectAll().ToArray();
            }
        }
    }

    public IReadOnlyList<DiscoveryChange> Upsert(DnsSdRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.DeviceId == _localDeviceId || !IsUsable(record)) return [];

        lock (_gate)
        {
            _sources.TryGetValue(record.SourceKey, out var previousSource);
            var affectedIds = previousSource is null || previousSource.DeviceId == record.DeviceId
                ? new[] { record.DeviceId }
                : new[] { previousSource.DeviceId, record.DeviceId };
            var before = affectedIds.ToDictionary(id => id, Project);
            _sources[record.SourceKey] = record;
            return ChangesFor(affectedIds, before);
        }
    }

    public IReadOnlyList<DiscoveryChange> RemoveSource(string sourceKey)
    {
        lock (_gate)
        {
            if (!_sources.TryGetValue(sourceKey, out var source)) return [];
            var before = Project(source.DeviceId);
            _sources.Remove(sourceKey);
            return Change(source.DeviceId, before, Project(source.DeviceId));
        }
    }

    public IReadOnlyList<DiscoveryChange> RemoveExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            var expired = _sources.Values.Where(value => value.ExpiresAtUtc <= now).ToArray();
            if (expired.Length == 0) return [];
            var affectedIds = expired.Select(value => value.DeviceId).Distinct().ToArray();
            var before = affectedIds.ToDictionary(id => id, Project);
            foreach (var source in expired) _sources.Remove(source.SourceKey);
            return ChangesFor(affectedIds, before);
        }
    }

    public void Clear()
    {
        lock (_gate) _sources.Clear();
    }

    private IReadOnlyList<DiscoveryChange> ChangesFor(
        IEnumerable<Guid> ids,
        IReadOnlyDictionary<Guid, DiscoveredDevice?> before) =>
        ids.SelectMany(id => Change(id, before[id], Project(id))).ToArray();

    private static IReadOnlyList<DiscoveryChange> Change(
        Guid id,
        DiscoveredDevice? before,
        DiscoveredDevice? after)
    {
        if (before is null && after is not null) return [new(DiscoveryChangeKind.Appeared, after)];
        if (before is not null && after is null) return [new(DiscoveryChangeKind.Disappeared, before)];
        if (before is not null && after is not null && before != after) return [new(DiscoveryChangeKind.Updated, after)];
        return [];
    }

    private IEnumerable<DiscoveredDevice> ProjectAll() =>
        _sources.Values.Select(value => value.DeviceId).Distinct().Select(Project).OfType<DiscoveredDevice>();

    private DiscoveredDevice? Project(Guid deviceId)
    {
        var source = _sources.Values
            .Where(value => value.DeviceId == deviceId)
            .OrderByDescending(value => value.ExpiresAtUtc)
            .ThenBy(value => value.SourceKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (source is null) return null;

        var address = DiscoveryAddressSelector.Select(source.Addresses, _localAddresses);
        return new(source.DeviceId, source.DeviceName, source.Platform, source.ProtocolVersion,
            source.AppVersion, address, source.Port);
    }

    private static bool IsUsable(DnsSdRecord record) =>
        record.DeviceId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(record.SourceKey) &&
        !string.IsNullOrWhiteSpace(record.DeviceName) &&
        !string.IsNullOrWhiteSpace(record.Platform) &&
        record.ProtocolVersion > 0 &&
        !string.IsNullOrWhiteSpace(record.AppVersion) &&
        record.Port is >= 1 and <= 65535 &&
        record.Addresses.Count > 0;
}
