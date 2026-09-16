using System.ComponentModel;
using System.Runtime.CompilerServices;
using Drop.Protocol;
using Drop.Routing;

namespace Drop.Windows;

public enum TransferState
{
    Idle,
    Connecting,
    WaitingForAcceptance,
    PreparingFile,
    Transferring,
    Completing,
    Completed,
    Failed,
    Cancelled
}

public sealed class TransferStateModel : INotifyPropertyChanged
{
    private TransferState _state = TransferState.Idle;
    private string _statusText = "Select a nearby device to send a file.";
    private string _fileName = string.Empty;
    private long _bytesTransferred;
    private long _totalBytes;
    private string? _remoteEndpoint;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TransferState State => _state;
    public string StatusText => _statusText;
    public string FileName => _fileName;
    public long BytesTransferred => _bytesTransferred;
    public long TotalBytes => _totalBytes;
    public bool IsBusy => _state is TransferState.Connecting or TransferState.WaitingForAcceptance
        or TransferState.PreparingFile or TransferState.Transferring or TransferState.Completing;
    public bool CanCancel => IsBusy;
    public double ProgressPercent => _totalBytes == 0
        ? (_state == TransferState.Completed ? 100 : 0)
        : Math.Clamp((double)_bytesTransferred / _totalBytes * 100, 0, 100);
    public string ProgressText => _totalBytes > 0
        ? $"{FormatBytes(_bytesTransferred)} / {FormatBytes(_totalBytes)}"
        : string.Empty;

    public void Begin(string fileName, long totalBytes, string? remoteEndpoint = null)
    {
        if (IsBusy) throw new InvalidOperationException("A transfer is already active.");
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (totalBytes < 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));

        _fileName = fileName;
        _bytesTransferred = 0;
        _totalBytes = totalBytes;
        _remoteEndpoint = remoteEndpoint;
        SetState(TransferState.Connecting, ConnectingStatus());
        NotifyProgress();
        OnPropertyChanged(nameof(FileName));
    }

    public void ReportStage(SendStage stage)
    {
        _ = stage switch
        {
            SendStage.Connecting => SetState(TransferState.Connecting, ConnectingStatus()),
            SendStage.WaitingForAcceptance => SetState(TransferState.WaitingForAcceptance, "Waiting for acceptance…"),
            SendStage.PreparingFile => SetState(TransferState.PreparingFile, "Accepted — preparing file…"),
            SendStage.Transferring => SetState(TransferState.Transferring, "Transferring…"),
            SendStage.Completing => SetState(TransferState.Completing, "Verifying completion…"),
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
    }

    public void ReportProgress(FileTransferProgress progress)
    {
        _bytesTransferred = Math.Clamp(progress.BytesTransferred, 0, progress.TotalBytes);
        _totalBytes = progress.TotalBytes;
        NotifyProgress();
    }

    public void Complete()
    {
        _bytesTransferred = _totalBytes;
        SetState(TransferState.Completed, "Completed");
        NotifyProgress();
    }

    public void Fail(string message) =>
        SetState(TransferState.Failed, $"Failed: {message}");

    public void Fail(ConnectionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.Kind == ConnectionFailureKind.Cancelled)
        {
            Cancel();
            return;
        }

        string message = failure.Kind switch
        {
            ConnectionFailureKind.NoEligibleRoute => "No available route to this device.",
            ConnectionFailureKind.RouteUnavailable => "The selected route is unavailable.",
            ConnectionFailureKind.UnsupportedTransport => "This connection method is not supported.",
            ConnectionFailureKind.ConnectionTimeout => "Connection timed out.",
            ConnectionFailureKind.ConnectionRefused => "The device refused the connection.",
            ConnectionFailureKind.AuthenticationFailed => "Device authentication failed.",
            ConnectionFailureKind.TransportInterrupted => "The connection was interrupted.",
            ConnectionFailureKind.ProtocolError => "The transfer protocol failed.",
            ConnectionFailureKind.RetryExhausted => "Could not connect after retrying.",
            _ => failure.Diagnostic ?? "Connection failed."
        };
        Fail(message);
    }

    public void Cancel() => SetState(TransferState.Cancelled, "Cancelled");

    private string ConnectingStatus() => string.IsNullOrWhiteSpace(_remoteEndpoint)
        ? "Connecting…"
        : $"Connecting to {_remoteEndpoint}…";

    private TransferState SetState(TransferState state, string status)
    {
        _state = state;
        _statusText = status;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanCancel));
        return state;
    }

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(BytesTransferred));
        OnPropertyChanged(nameof(TotalBytes));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
