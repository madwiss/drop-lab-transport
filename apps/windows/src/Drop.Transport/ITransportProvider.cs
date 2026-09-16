namespace Drop.Transport;

/// <summary>
/// Provides a transport implementation and describes its capabilities.
/// </summary>
public interface ITransportProvider
{
    string TransportId { get; }

    TransportCapability Capability { get; }

    bool IsAvailable { get; }

    IReadOnlyCollection<TransportCandidate> DiscoverCandidates(
        TransportDiscoveryContext context);

    ITransportEndpoint CreateEndpoint(
        TransportEndpointDescriptor descriptor);

    ITransportConnector CreateConnector();

    ITransportListener CreateListener();
}