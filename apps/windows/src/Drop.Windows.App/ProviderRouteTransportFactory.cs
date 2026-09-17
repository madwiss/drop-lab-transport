using Drop.Discovery;
using Drop.Routing;
using Drop.Transport;

namespace Drop.Windows;

public sealed class ProviderRouteTransportFactory : IRouteTransportFactory
{
    private readonly TransportProviderResolver _resolver;

    public ProviderRouteTransportFactory(
        TransportProviderResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _resolver = resolver;
    }

    public ITransportConnector CreateConnector(
        ConnectionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        ITransportProvider provider =
            ResolveProvider(candidate);

        return provider.CreateConnector();
    }

    public ITransportEndpoint CreateEndpoint(
        ConnectionCandidate candidate,
        DiscoveredDevice device)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(device);

        ITransportProvider provider =
            ResolveProvider(candidate);

        TransportEndpointDescriptor descriptor =
            CreateDescriptor(candidate, device);

        return provider.CreateEndpoint(descriptor);
    }

    public StartedTransportListener CreateStartedListener()
{
    ITransportProvider provider =
        _resolver.GetBestAvailable();

    ITransportListener listener =
        provider.CreateListener();

    return new StartedTransportListener(
        listener,
        listener.LocalEndpoint);
}
    public int GetListenerPort(ITransportEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return endpoint switch
        {
            TcpTransportEndpoint tcp => tcp.Port,
            _ => throw new NotSupportedException("Transport endpoint does not expose a discovery port.")
        };
    }

private ITransportProvider ResolveProvider(
        ConnectionCandidate candidate)
    {
        return candidate.TransportKind switch
        {
            TransportKind.ReliableByteStream =>
                _resolver.GetBestAvailable(),

            _ =>
                throw new NotSupportedException(
                    $"Transport '{candidate.TransportKind}' is not supported.")
        };
    }

    private static TransportEndpointDescriptor CreateDescriptor(
        ConnectionCandidate candidate,
        DiscoveredDevice device)
    {
        return candidate.TransportKind switch
        {
            TransportKind.ReliableByteStream =>
                new TcpTransportEndpointDescriptor(
                    device.Address,
                    device.Port),

            _ =>
                throw new NotSupportedException(
                    $"Transport '{candidate.TransportKind}' is not supported.")
        };
    }
}












