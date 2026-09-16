namespace Drop.Protocol;

/// <summary>
/// Device metadata exchanged during the Protocol v1 handshake.
/// </summary>
public sealed record DeviceInfo(
    Guid DeviceId,
    string Name,
    string Platform,
    string AppVersion,
    string? Fingerprint = null);
