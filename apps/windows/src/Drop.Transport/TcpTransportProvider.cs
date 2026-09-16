using System.Net;

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

    public ITransportEndpoint CreateEndpoint(
        TransportEndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor is not TcpTransportEndpointDescriptor tcp)
        {
            throw new ArgumentException(
                "TCP transport requires a TCP endpoint descriptor.",
                nameof(descriptor));
        }

        return new TcpTransportEndpoint(tcp.Address, tcp.Port);
    }

    public ITransportConnector CreateConnector() =>
        new TcpTransportConnector();

    public ITransportListener CreateListener() =>
        new TcpTransportListener(IPAddress.Any);
}