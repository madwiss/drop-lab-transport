using System.Net;
using System.Net.Sockets;

namespace Drop.Transport;

public sealed record TcpTransportEndpoint(IPAddress Address, int Port) : ITransportEndpoint
{
    public TcpTransportEndpoint(IPEndPoint endpoint) : this(
        endpoint?.Address ?? throw new ArgumentNullException(nameof(endpoint)),
        endpoint.Port)
    {
    }

    public IPEndPoint ToIPEndPoint() => new(Address, Port);

    public override string ToString() => ToIPEndPoint().ToString();
}

public sealed class TcpTransportConnector : ITransportConnector
{
    public async ValueTask<IReliableByteStream> ConnectAsync(
        ITransportEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint is not TcpTransportEndpoint tcpEndpoint)
        {
            throw new ArgumentException("TCP transport requires a TCP endpoint.", nameof(endpoint));
        }

        TcpClient client = new(tcpEndpoint.Address.AddressFamily);
        try
        {
            await client.ConnectAsync(tcpEndpoint.Address, tcpEndpoint.Port, cancellationToken)
                .ConfigureAwait(false);
            return new TcpReliableByteStream(client);
        }
        catch (SocketException ex)
        {
            client.Dispose();
            throw new TransportFailureException(
                TransportFailureKind.Connection,
                $"TCP connection to {tcpEndpoint} failed.",
                ex);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}

public sealed class TcpTransportListener : ITransportListener
{
    private readonly TcpListener _listener;
    private bool _started;

    public TcpTransportListener(IPAddress address, int port = 0, bool dualMode = false)
    {
        ArgumentNullException.ThrowIfNull(address);
        _listener = new TcpListener(address, port);
        if (dualMode)
        {
            _listener.Server.DualMode = true;
        }
    }

    public ITransportEndpoint LocalEndpoint
    {
        get
        {
            if (!_started)
            {
                throw new InvalidOperationException("The listener has not been started.");
            }

            return new TcpTransportEndpoint((IPEndPoint)_listener.LocalEndpoint);
        }
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _listener.Start();
        _started = true;
    }

    public async ValueTask<IReliableByteStream> AcceptAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            throw new InvalidOperationException("The listener has not been started.");
        }

        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        return new TcpReliableByteStream(client);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Stop();
        _started = false;
        return ValueTask.CompletedTask;
    }
}

internal sealed class TcpReliableByteStream(TcpClient client) : IReliableByteStream
{
    private readonly TcpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly NetworkStream _stream = client.GetStream();

    public Stream Stream => _stream;

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}

