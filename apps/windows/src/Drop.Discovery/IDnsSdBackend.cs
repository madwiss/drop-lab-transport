namespace Drop.Discovery;

internal interface IDnsSdBackend : IAsyncDisposable
{
    event EventHandler<DnsSdRecord>? RecordReceived;
    event EventHandler<string>? RecordRemoved;

    Task StartAsync(DropAdvertisement advertisement, CancellationToken cancellationToken);
    void Query();
    Task StopAsync(CancellationToken cancellationToken);
}
