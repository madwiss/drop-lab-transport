namespace Drop.Transport;

/// <summary>
/// TCP based transport provider.
/// </summary>
public sealed class TcpTransportProvider : ITransportProvider
{
    public string TransportId => "tcp-lan";

    public TransportCapability Capability { get; } =
        new(
            transportId: "tcp-lan",
            speedScore: 90,
            rangeScore: 40,
            securityScore: 70,
            batteryCostScore: 80,
            requiresNetwork: true,
            supportsLargeTransfers: true);

    public bool IsAvailable => true;

    public IReadOnlyCollection<TransportCandidate> DiscoverCandidates() =>
        Array.Empty<TransportCandidate>();

    public ITransportConnector CreateConnector() =>
        new TcpTransportConnector();

    public ITransportListener CreateListener() =>
        new TcpTransportListener(System.Net.IPAddress.Any);
}