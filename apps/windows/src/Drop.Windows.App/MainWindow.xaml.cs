using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Drop.Discovery;
using Drop.Protocol;
using Microsoft.Win32;

namespace Drop.Windows;

public partial class MainWindow : Window
{
    private const string AppVersion = "0.1.0";
    private readonly ObservableCollection<DiscoveredDevice> _devices = [];
    private readonly LanDiscoveryService _discovery = new();
    private readonly TransferStateModel _transfer = new();
    private readonly CancellationTokenSource _lifetime = new();
    private ReceiverHost? _receiver;
    private DeviceInfo? _localDevice;
    private CancellationTokenSource? _sendCancellation;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new { Transfer = _transfer };
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
            _localDevice = new DeviceInfo(deviceId, Environment.MachineName, "windows", AppVersion);
            _receiver = new ReceiverHost(_localDevice);
            int port = _receiver.Start();
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
        _sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _transfer.Begin(file.Name, file.Length);
        UpdateActions();

        try
        {
            Progress<FileTransferProgress> progress = new(_transfer.ReportProgress);
            Progress<SendStage> stages = new(_transfer.ReportStage);
            TcpFileSender senderCore = new(_localDevice);
            await senderCore.SendAsync(
                new IPEndPoint(device.Address, device.Port), file.FullName, file.Name,
                progress, _sendCancellation.Token, stages);
            _transfer.Complete();
        }
        catch (OperationCanceledException) when (_sendCancellation.IsCancellationRequested)
        {
            _transfer.Cancel();
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
        await _discovery.DisposeAsync();
        if (_receiver is not null) await _receiver.DisposeAsync();
        _lifetime.Dispose();
    }
}
