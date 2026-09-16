using System.ComponentModel;
using System.Runtime.CompilerServices;
using Drop.Protocol;
using Drop.Routing;

namespace Drop.Windows;

public enum IncomingTransferState
{
    Idle,
    IncomingOffer,
    Receiving,
    Completed,
    Failed,
    Declined,
    Cancelled
}

public sealed class IncomingTransferStateModel : INotifyPropertyChanged
{
    private IncomingTransferState _state;
    private string _senderName = string.Empty;
    private string _fileName = string.Empty;
    private long _bytesReceived;
    private long _totalBytes;
    private string _statusText = string.Empty;
    private TaskCompletionSource<IncomingTransferDecision>? _decision;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IncomingTransferState State => _state;
    public string SenderName => _senderName;
    public string FileName => _fileName;
    public long BytesReceived => _bytesReceived;
    public long TotalBytes => _totalBytes;
    public string StatusText => _statusText;
    public bool IsVisible => _state != IncomingTransferState.Idle;
    public bool CanDecide => _state == IncomingTransferState.IncomingOffer;
    public double ProgressPercent => _totalBytes == 0
        ? (_state == IncomingTransferState.Completed ? 100 : 0)
        : Math.Clamp((double)_bytesReceived / _totalBytes * 100, 0, 100);
    public string ProgressText => $"{FormatBytes(_bytesReceived)} / {FormatBytes(_totalBytes)}";

    public Task<IncomingTransferDecision> PresentAsync(
        IncomingTransferOffer offer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (_decision is not null)
        {
            throw new InvalidOperationException("An incoming transfer decision is already pending.");
        }

        _senderName = offer.Sender.Name;
        _fileName = offer.FileName;
        _bytesReceived = 0;
        _totalBytes = offer.FileSize;
        _decision = new TaskCompletionSource<IncomingTransferDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetState(IncomingTransferState.IncomingOffer, "Incoming file");
        OnPropertyChanged(nameof(SenderName));
        OnPropertyChanged(nameof(FileName));
        NotifyProgress();

        return AwaitDecisionAsync(_decision, cancellationToken);
    }

    public void Accept()
    {
        TaskCompletionSource<IncomingTransferDecision>? decision = _decision;
        if (decision is null) return;

        SetState(IncomingTransferState.Receiving, "Receiving…");
        decision.TrySetResult(IncomingTransferDecision.Accept);
    }

    public void Decline()
    {
        TaskCompletionSource<IncomingTransferDecision>? decision = _decision;
        if (decision is null) return;

        SetState(IncomingTransferState.Declined, "Declined");
        decision.TrySetResult(IncomingTransferDecision.Decline);
    }

    public void ReportProgress(FileTransferProgress progress)
    {
        _bytesReceived = Math.Clamp(progress.BytesTransferred, 0, progress.TotalBytes);
        _totalBytes = progress.TotalBytes;
        SetState(IncomingTransferState.Receiving, "Receiving…");
        NotifyProgress();
    }

    public void Complete()
    {
        _bytesReceived = _totalBytes;
        SetState(IncomingTransferState.Completed, "Completed");
        NotifyProgress();
    }

    public void Fail(string message) =>
        SetState(IncomingTransferState.Failed, $"Failed: {message}");

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
            ConnectionFailureKind.ConnectionTimeout => "Transfer timed out.",
            ConnectionFailureKind.TransportInterrupted => "The connection was interrupted.",
            ConnectionFailureKind.ProtocolError => "The transfer protocol failed.",
            ConnectionFailureKind.RetryExhausted => "The connection failed after retrying.",
            _ => failure.Diagnostic ?? "Transfer failed."
        };
        Fail(message);
    }

    public void Cancel() => SetState(IncomingTransferState.Cancelled, "Cancelled");

    private async Task<IncomingTransferDecision> AwaitDecisionAsync(
        TaskCompletionSource<IncomingTransferDecision> decision,
        CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => decision.TrySetCanceled(cancellationToken));
        try
        {
            return await decision.Task.ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_decision, decision)) _decision = null;
        }
    }

    private void SetState(IncomingTransferState state, string status)
    {
        _state = state;
        _statusText = status;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(CanDecide));
    }

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(BytesReceived));
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
