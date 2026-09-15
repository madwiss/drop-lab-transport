namespace Drop.Security;

public sealed class DeviceIdentityProvider
{
    private readonly IDeviceIdentityStore store;

    public DeviceIdentityProvider(IDeviceIdentityStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    public async ValueTask<DeviceIdentity> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        byte[]? persisted = await store.LoadPrivateIdentityAsync(cancellationToken).ConfigureAwait(false);
        if (persisted is not null)
            return DeviceIdentity.ImportPrivateIdentity(persisted);

        DeviceIdentity identity = DeviceIdentity.Generate();
        try
        {
            await store.SavePrivateIdentityAsync(identity.ExportPrivateIdentity(), cancellationToken)
                .ConfigureAwait(false);
            return identity;
        }
        catch
        {
            identity.Dispose();
            throw;
        }
    }
}
