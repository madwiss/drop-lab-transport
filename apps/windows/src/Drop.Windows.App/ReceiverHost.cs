using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;
using Drop.Protocol;

namespace Drop.Windows;

internal sealed class ReceiverHost(
    DeviceInfo localDevice,
    Func<IncomingTransferOffer, CancellationToken, ValueTask<IncomingTransferDecision>> decide,
    IProgress<FileTransferProgress> progress) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentDictionary<int, Task> _sessions = new();
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private int _nextSessionId;

    public event EventHandler<ReceiveSessionResult>? SessionEnded;
    public event EventHandler<Exception>? SessionFailed;

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
        await Task.WhenAll(_sessions.Values).ConfigureAwait(false);
        _sessionGate.Dispose();
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

            int sessionId = Interlocked.Increment(ref _nextSessionId);
            Task session = ReceiveSafelyAsync(client, cancellationToken);
            _sessions[sessionId] = session;
            _ = session.ContinueWith(
                completedTask => _sessions.TryRemove(sessionId, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task ReceiveSafelyAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string destination = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Drop");
        bool enteredGate = false;
        try
        {
            await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            enteredGate = true;
            ReceiveSessionResult result = await new TcpFileReceiver(localDevice)
                .ReceiveAsync(client, destination, decide, progress, cancellationToken)
                .ConfigureAwait(false);
            SessionEnded?.Invoke(this, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SessionFailed?.Invoke(this, ex);
        }
        finally
        {
            client.Dispose();
            if (enteredGate) _sessionGate.Release();
        }
    }
}
