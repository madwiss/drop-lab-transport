namespace Drop.Transport;

/// <summary>
/// Registry of available transport providers.
/// </summary>
public sealed class TransportRegistry
{
    private readonly Dictionary<string, ITransportProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IReadOnlyCollection<ITransportProvider> Providers =>
        _providers.Values.ToArray();

    /// <summary>
    /// Registers a transport provider.
    /// </summary>
    public void Register(ITransportProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.TransportId))
        {
            throw new ArgumentException(
                "Transport provider must have a valid identifier.",
                nameof(provider));
        }

        if (!_providers.TryAdd(provider.TransportId, provider))
        {
            throw new InvalidOperationException(
                $"A transport provider with id '{provider.TransportId}' is already registered.");
        }
    }

    /// <summary>
    /// Returns a provider by identifier.
    /// </summary>
    public ITransportProvider? Get(string transportId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportId);

        return _providers.TryGetValue(transportId, out var provider)
            ? provider
            : null;
    }

    /// <summary>
    /// Returns providers that are currently available.
    /// </summary>
    public IReadOnlyCollection<ITransportProvider> GetAvailable()
    {
        return _providers.Values
            .Where(provider => provider.IsAvailable)
            .ToArray();
    }
}