namespace Drop.Transport;

/// <summary>
/// Resolves transport providers from a registry.
/// </summary>
public sealed class TransportProviderResolver
{
    private readonly TransportRegistry _registry;

    public TransportProviderResolver(TransportRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public ITransportProvider GetRequired(string transportId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportId);

        return _registry.Get(transportId)
            ?? throw new InvalidOperationException(
                $"No transport provider registered with id '{transportId}'.");
    }
}