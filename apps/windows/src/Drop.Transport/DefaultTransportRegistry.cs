namespace Drop.Transport;

/// <summary>
/// Creates the default transport registry used by the application.
/// </summary>
public static class DefaultTransportRegistry
{
    public static TransportRegistry Create()
    {
        var registry = new TransportRegistry();

        registry.Register(new TcpTransportProvider());

        return registry;
    }
}