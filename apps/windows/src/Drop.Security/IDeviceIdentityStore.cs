namespace Drop.Security;

public interface IDeviceIdentityStore
{
    ValueTask<byte[]?> LoadPrivateIdentityAsync(CancellationToken cancellationToken = default);

    ValueTask SavePrivateIdentityAsync(byte[] privateIdentity, CancellationToken cancellationToken = default);
}
