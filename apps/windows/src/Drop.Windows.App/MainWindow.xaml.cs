using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Drop.Discovery;
using Drop.Protocol;
using Drop.Routing;
using Drop.Security;
using Drop.Transport;
using Microsoft.Win32;

namespace Drop.Windows;

public partial class MainWindow : Window
{
    private const string AppVersion = "0.1.0";
    private readonly ObservableCollection<DiscoveredDevice> _devices = [];
    private readonly LanDiscoveryService _discovery = new();
    private readonly TransferStateModel _transfer = new();
    private readonly IncomingTransferStateModel _incoming = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly IRouteTransportFactory _transportFactory;

    private readonly TransportProviderResolver _transportResolver =
        new(DefaultTransportRegistry.Create());
    private readonly PeerTrustService _peerTrust = new(new JsonTrustedPeerStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Drop", "trusted-peers.json")));
    private ReceiverHost? _receiver;
    private DeviceInfo? _localDevice;
    private CancellationTokenSource? _sendCancellation;

    public MainWindow()
{
    _transportFactory = new ProviderRouteTransportFactory(_transportResolver);

    InitializeComponent();
    DataContext = new { Transfer = _transfer, Incoming = _incoming };
    DeviceList.ItemsSource = _devices;
    _transfer.PropertyChanged += Transfer_PropertyChanged;
    _discovery.DeviceAppeared += Discovery_DeviceAppeared;
    _discovery.DeviceUpdated += Discovery_DeviceUpdated;
    _discovery.DeviceDisappeared += Discovery_DeviceDisappeared;
    Loaded += MainWindow_Loaded;
    Closing += MainWindow_Closing;
    Closed += MainWindow_Closed;
}

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Guid deviceId = LocalDeviceIdentity.LoadOrCreate();
            _localDevice = new DeviceInfo(deviceId, Environment.MachineName, "windows", AppVersion, deviceId.ToString("D"));
            Progress<FileTransferProgress> receiveProgress = new(_incoming.ReportProgress);
            _receiver = new ReceiverHost(
                _localDevice,
                DecideIncomingAsync,
                receiveProgress,
                _transportFactory.CreateStartedListener);
            _receiver.SessionEnded += Receiver_SessionEnded;
            _receiver.SessionFailed += Receiver_SessionFailed;
            ITransportEndpoint listenerEndpoint = _receiver.Start();
            if (listenerEndpoint is not TcpTransportEndpoint tcpListenerEndpoint)
            {
                throw new NotSupportedException("The LAN discovery advertisement requires a TCP listener endpoint.");
            }
            int port = tcpListenerEndpoint.Port;
            await _discovery.StartAsync(new DropAdvertisement(
                deviceId, _localDevice.Name, _localDevice.Platform, 1, AppVersion, port), _lifetime.Token);
            RefreshDiscoveryText();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            DiscoveryStatusText.Text = $"Discovery failed: {ex.Message}";
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sendCancellation is not null || DeviceList.SelectedItem is not DiscoveredDevice device || _localDevice is null)
        {
            return;
        }

        OpenFileDialog picker = new() { Title = $"Send a file to {device.DeviceName}", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;

        FileInfo file = new(picker.FileName);
        SelectedTransportRoute selectedRoute;
        try
        {
            selectedRoute = LanRouteAdapter.Select(device, _transportFactory);
        }
        catch (Exception ex)
        {
            _transfer.Begin(file.Name, file.Length);
            _transfer.Fail(new ConnectionFailure(
                ConnectionFailureKind.NoEligibleRoute,
                IsRetryable: false,
                ex.Message));
            UpdateActions();
            return;
        }

        _sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _transfer.Begin(file.Name, file.Length, selectedRoute.Endpoint.ToString());
        Trace.WriteLine($"Drop send selected route {selectedRoute.Candidate.CandidateId} endpoint {selectedRoute.Endpoint} for device {device.DeviceId:D} ({device.DeviceName}).");
        UpdateActions();

        try
        {
            Progress<FileTransferProgress> progress = new(_transfer.ReportProgress);
            Progress<SendStage> stages = new(_transfer.ReportStage);
            TcpFileSender senderCore = new(_localDevice, selectedRoute.Connector);
            await senderCore.SendAsync(
                selectedRoute.Endpoint, file.FullName, file.Name,
                progress, _sendCancellation.Token, stages);
            _transfer.Complete();
        }
        catch (TransferFailedException ex) when (ex.Kind == TransferFailureKind.Cancelled)
        {
            _transfer.Cancel();
        }
        catch (OperationCanceledException) when (_sendCancellation.IsCancellationRequested)
        {
            _transfer.Cancel();
        }
        catch (TransferFailedException ex)
        {
            _transfer.Fail(ConnectionFailureMapper.FromTransferFailure(ex));
        }
        catch (Exception ex)
        {
            _transfer.Fail(ex.Message);
        }
        finally
        {
            _sendCancellation.Dispose();
            _sendCancellation = null;
            UpdateActions();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _sendCancellation?.Cancel();

    private async void AcceptIncomingButton_Click(object sender, RoutedEventArgs e)
    {
        IncomingTransferOffer? offer = _incoming.CurrentOffer;
        _incoming.Accept();

        if (offer is not null)
        {
            await _peerTrust.TrustAfterAcceptedAsync(
                offer.Sender.DeviceId.ToString("D"),
                GetPeerFingerprint(offer.Sender),
                offer.Sender.Name);
        }
    }

    private void DeclineIncomingButton_Click(object sender, RoutedEventArgs e) => _incoming.Decline();

    private async ValueTask<IncomingTransferDecision> DecideIncomingAsync(
        IncomingTransferOffer offer,
        CancellationToken cancellationToken)
    {
        string deviceId = offer.Sender.DeviceId.ToString("D");
        string fingerprint = GetPeerFingerprint(offer.Sender);
        if (await _peerTrust.IsTrustedAsync(deviceId, fingerprint, cancellationToken))
        {
            return IncomingTransferDecision.Accept;
        }

        Task<IncomingTransferDecision> decision = await Dispatcher.InvokeAsync(
            () => _incoming.PresentAsync(offer, cancellationToken));
        return await decision.ConfigureAwait(false);
    }

    private static string GetPeerFingerprint(DeviceInfo device) =>
        !string.IsNullOrWhiteSpace(device.Fingerprint)
            ? device.Fingerprint
            : device.DeviceId.ToString("D");

    private void Receiver_SessionEnded(object? sender, ReceiveSessionResult result) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (result.Success) _incoming.Complete();
            else if (result.ErrorCode != "DECLINED") _incoming.Fail(result.ErrorCode ?? "Transfer failed");
        });

    private void Receiver_SessionFailed(object? sender, Exception exception) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (_lifetime.IsCancellationRequested) return;
            if (exception is TransferFailedException transferFailure)
                _incoming.Fail(ConnectionFailureMapper.FromTransferFailure(transferFailure));
            else
                _incoming.Fail(exception.Message);
        });

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    private void Discovery_DeviceAppeared(object? sender, DiscoveredDeviceEventArgs e) =>
        Dispatcher.InvokeAsync(() =>
        {
            UpsertDevice(e.Device);
            RefreshDiscoveryText();
        });

    private void Discovery_DeviceUpdated(object? sender, DiscoveredDeviceEventArgs e) =>
        Dispatcher.InvokeAsync(() => UpsertDevice(e.Device));

    private void Discovery_DeviceDisappeared(object? sender, DiscoveredDeviceEventArgs e) =>
        Dispatcher.InvokeAsync(() =>
        {
            DiscoveredDevice? existing = _devices.FirstOrDefault(item => item.DeviceId == e.Device.DeviceId);
            bool wasSelected = existing is not null && ReferenceEquals(DeviceList.SelectedItem, existing);
            if (existing is not null) _devices.Remove(existing);
            if (wasSelected && !_transfer.IsBusy) DiscoveryStatusText.Text = "The selected device is no longer available.";
            else RefreshDiscoveryText();
            UpdateActions();
        });

    private void UpsertDevice(DiscoveredDevice device)
    {
        DiscoveredDevice? existing = _devices.FirstOrDefault(item => item.DeviceId == device.DeviceId);
        bool selected = existing is not null && ReferenceEquals(DeviceList.SelectedItem, existing);
        if (existing is not null) _devices.Remove(existing);
        _devices.Add(device);
        if (selected) DeviceList.SelectedItem = device;
        UpdateActions();
    }

    private void RefreshDiscoveryText() => DiscoveryStatusText.Text = _devices.Count == 0
        ? "Looking for devices…"
        : $"{_devices.Count} device{(_devices.Count == 1 ? string.Empty : "s")} available";

    private void Transfer_PropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateActions();

    private void UpdateActions()
    {
        SendButton.IsEnabled = !_transfer.IsBusy && _sendCancellation is null && DeviceList.SelectedItem is DiscoveredDevice;
        CancelButton.IsEnabled = _transfer.CanCancel && _sendCancellation is not null;
        DeviceList.IsEnabled = !_transfer.IsBusy;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _sendCancellation?.Cancel();
        _lifetime.Cancel();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _discovery.DeviceAppeared -= Discovery_DeviceAppeared;
        _discovery.DeviceUpdated -= Discovery_DeviceUpdated;
        _discovery.DeviceDisappeared -= Discovery_DeviceDisappeared;
        if (_receiver is not null)
        {
            _receiver.SessionEnded -= Receiver_SessionEnded;
            _receiver.SessionFailed -= Receiver_SessionFailed;
        }
        await _discovery.DisposeAsync();
        if (_receiver is not null) await _receiver.DisposeAsync();
        _lifetime.Dispose();
    }
}

public sealed record SelectedTransportRoute(
    ConnectionCandidate Candidate,
    ITransportEndpoint Endpoint,
    ITransportConnector Connector);

public sealed record StartedTransportListener(
    ITransportListener Listener,
    ITransportEndpoint LocalEndpoint);

public interface IRouteTransportFactory
{
    ITransportConnector CreateConnector(ConnectionCandidate candidate);

    ITransportEndpoint CreateEndpoint(ConnectionCandidate candidate, DiscoveredDevice device);

    StartedTransportListener CreateStartedListener();
}

public sealed class LanTcpRouteTransportFactory : IRouteTransportFactory
{
    public ITransportConnector CreateConnector(ConnectionCandidate candidate)
    {
        EnsureLanReliableStream(candidate);
        return new TcpTransportConnector();
    }

    public ITransportEndpoint CreateEndpoint(ConnectionCandidate candidate, DiscoveredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        EnsureLanReliableStream(candidate);
        return new TcpTransportEndpoint(device.Address, device.Port);
    }

    public StartedTransportListener CreateStartedListener()
    {
        TcpTransportListener listener = new(IPAddress.IPv6Any, 0, dualMode: true);
        listener.Start();
        return new StartedTransportListener(listener, listener.LocalEndpoint);
    }

    private static void EnsureLanReliableStream(ConnectionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.RouteKind != RouteKind.LocalLan ||
            candidate.TransportKind != TransportKind.ReliableByteStream)
        {
            throw new NotSupportedException(
                $"Route '{candidate.CandidateId}' is not supported by the LAN TCP transport adapter.");
        }
    }
}

public static class LanRouteAdapter
{
    private const RouteCapabilities LanCapabilities =
        RouteCapabilities.Reliable |
        RouteCapabilities.Ordered |
        RouteCapabilities.Bidirectional |
        RouteCapabilities.SupportsLargeTransfers;

    public static PeerRoutes ToPeerRoutes(DiscoveredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ConnectionCandidate candidate = new(
            $"lan:{device.DeviceId:D}",
            RouteKind.LocalLan,
            TransportKind.ReliableByteStream,
            LanCapabilities,
            CandidateAvailability.Reachable);
        return new PeerRoutes(device.DeviceId.ToString("D"), [candidate]);
    }

    public static SelectedTransportRoute Select(
        DiscoveredDevice device,
        IRouteTransportFactory transportFactory)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(transportFactory);

        PeerRoutes routes = ToPeerRoutes(device);
        ConnectionCandidate candidate = RouteSelector.SelectPreferred(routes, LanCapabilities)
            ?? throw new InvalidOperationException("No eligible route is available for this device.");

        return new SelectedTransportRoute(
            candidate,
            transportFactory.CreateEndpoint(candidate, device),
            transportFactory.CreateConnector(candidate));
    }
}
