namespace Drop.Transport;

/// <summary>
/// Describes a connection possibility exposed by a transport.
/// </summary>
public sealed record TransportCandidate
{
    public TransportCandidate(
        string candidateId,
        string transportId,
        ITransportEndpoint endpoint,
        TransportCapability capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportId);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(capability);

        CandidateId = candidateId;
        TransportId = transportId;
        Endpoint = endpoint;
        Capability = capability;
    }

    public string CandidateId { get; }

    public string TransportId { get; }

    public ITransportEndpoint Endpoint { get; }

    public TransportCapability Capability { get; }
}