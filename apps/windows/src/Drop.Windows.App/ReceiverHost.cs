using System.Net;
using System.Net.Sockets;
using System.IO;
using Drop.Protocol;

namespace Drop.Windows;

internal sealed class ReceiverHost(DeviceInfo localDevice) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private TcpListener? _listener;
    private Task? _acceptLoop;

    public int Start()
    {
        _listener = new TcpListener(IPAddress.IPv6Any, 0);
        _listener.Server.DualMode = true;
        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_listener, _cancellation.Token);
        return ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _listener?.Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cancellation.Dispose();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = ReceiveSafelyAsync(client, cancellationToken);
        }
    }

    private async Task ReceiveSafelyAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string destination = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Drop");
        try
        {
            await new TcpFileReceiver(localDevice)
                .ReceiveAsync(client, destination, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            client.Dispose();
        }
    }
}
