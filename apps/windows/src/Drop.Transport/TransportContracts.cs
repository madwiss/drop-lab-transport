namespace Drop.Transport;

/// <summary>
/// Marker for an address understood by a concrete transport adapter.
/// </summary>
public interface ITransportEndpoint;

/// <summary>
/// An established reliable, ordered, bidirectional byte stream.
/// </summary>
public interface IReliableByteStream : IAsyncDisposable
{
    Stream Stream { get; }
}

/// <summary>
/// Establishes an outbound reliable byte stream to a transport-specific endpoint.
/// </summary>
public interface ITransportConnector
{
    ValueTask<IReliableByteStream> ConnectAsync(
        ITransportEndpoint endpoint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Accepts inbound reliable byte streams.
/// </summary>
public interface ITransportListener : IAsyncDisposable
{
    ValueTask<IReliableByteStream> AcceptAsync(CancellationToken cancellationToken = default);
}
