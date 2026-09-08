using System.Net;

namespace Drop.Discovery;

public sealed record DropAdvertisement(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    int ProtocolVersion,
    string AppVersion,
    int Port)
{
    internal void Validate()
    {
        if (DeviceId == Guid.Empty) throw new ArgumentException("DeviceId must not be empty.", nameof(DeviceId));
        if (string.IsNullOrWhiteSpace(DeviceName)) throw new ArgumentException("DeviceName is required.", nameof(DeviceName));
        if (string.IsNullOrWhiteSpace(Platform)) throw new ArgumentException("Platform is required.", nameof(Platform));
        if (ProtocolVersion <= 0) throw new ArgumentOutOfRangeException(nameof(ProtocolVersion));
        if (string.IsNullOrWhiteSpace(AppVersion)) throw new ArgumentException("AppVersion is required.", nameof(AppVersion));
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
    }
}

public sealed record DiscoveredDevice(
    Guid DeviceId,
    string DeviceName,
    string Platform,
    int ProtocolVersion,
    string AppVersion,
    IPAddress Address,
    int Port);

public sealed class DiscoveredDeviceEventArgs(DiscoveredDevice device) : EventArgs
{
    public DiscoveredDevice Device { get; } = device;
}
