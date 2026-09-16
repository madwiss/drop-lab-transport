namespace Drop.Transport;

/// <summary>
/// Provides a transport implementation and describes its capabilities.
/// </summary>
public interface ITransportProvider
{
    /// <summary>
    /// Stable identifier of this transport.
    /// Example: tcp-lan, bluetooth, wifi-direct.
    /// </summary>
    string TransportId { get; }

    /// <summary>
    /// Capabilities exposed by this transport.
    /// </summary>
    TransportCapability Capability { get; }

    /// <summary>
    /// Indicates whether this transport can currently be used.
    /// </summary>
    bool IsAvailable { get; }

    IReadOnlyCollection<TransportCandidate> DiscoverCandidates();

    /// <summary>
    /// Creates a connector for outbound connections.
    /// </summary>
    ITransportConnector CreateConnector();

    /// <summary>
    /// Creates a listener for inbound connections.
    /// </summary>
    ITransportListener CreateListener();
}