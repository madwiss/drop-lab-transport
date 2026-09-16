namespace Drop.Transport;

/// <summary>
/// Transport-neutral information used by providers
/// to discover connection candidates.
/// </summary>
public sealed record TransportDiscoveryContext(
    string RemoteDeviceId,
    string RemoteAddress,
    int RemotePort);