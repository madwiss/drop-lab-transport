namespace Drop.Transport;

/// <summary>
/// Resolves transport candidates from registered providers.
/// </summary>
public sealed class TransportCandidateResolver
{
    private readonly TransportRegistry _registry;

    public TransportCandidateResolver(TransportRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    /// <summary>
    /// Returns all available transport candidates from registered providers.
    /// </summary>
    public IReadOnlyCollection<TransportCandidate> Resolve()
    {
        return _registry
            .GetAvailable()
            .SelectMany(provider => provider.DiscoverCandidates())
            .ToArray();
    }
}