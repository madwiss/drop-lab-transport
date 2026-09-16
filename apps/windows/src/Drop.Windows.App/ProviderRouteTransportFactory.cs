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
            _resolver.GetRequired("tcp-lan");

        ITransportListener listener =
            provider.CreateListener();

        if (listener is not TcpTransportListener tcpListener)
        {
            throw new InvalidOperationException(
                "TCP transport listener was expected.");
        }

        return new StartedTransportListener(
            tcpListener,
            tcpListener.LocalEndpoint);
    }

    private ITransportProvider ResolveProvider(
        ConnectionCandidate candidate)
    {
        return candidate.TransportKind switch
        {
            TransportKind.ReliableByteStream =>
                _resolver.GetRequired("tcp-lan"),

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