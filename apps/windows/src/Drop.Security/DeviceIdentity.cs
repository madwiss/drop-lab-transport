using System.Security.Cryptography;

namespace Drop.Security;

public sealed class DeviceIdentity : IDisposable
{
    private const int P256PublicKeySizeBytes = 65;
    private readonly ECDsa signingKey;
    private bool disposed;

    private DeviceIdentity(ECDsa signingKey)
    {
        this.signingKey = signingKey;
        PublicIdentity = new DevicePublicIdentity(signingKey.ExportSubjectPublicKeyInfo());
    }

    public DevicePublicIdentity PublicIdentity { get; }

    public string SecurityId => PublicIdentity.SecurityId;

    public static DeviceIdentity Generate()
    {
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new DeviceIdentity(key);
    }

    public static DeviceIdentity ImportPrivateIdentity(ReadOnlySpan<byte> privateIdentity)
    {
        if (privateIdentity.IsEmpty)
            throw new ArgumentException("Private identity material cannot be empty.", nameof(privateIdentity));

        ECDsa key = ECDsa.Create();
        try
        {
            key.ImportPkcs8PrivateKey(privateIdentity, out int bytesRead);
            if (bytesRead != privateIdentity.Length)
                throw new CryptographicException("Private identity contains trailing data.");

            return new DeviceIdentity(key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public byte[] ExportPrivateIdentity()
    {
        ThrowIfDisposed();
        return signingKey.ExportPkcs8PrivateKey();
    }

    public byte[] SignChallenge(ReadOnlySpan<byte> challenge)
    {
        ThrowIfDisposed();
        if (challenge.IsEmpty)
            throw new ArgumentException("Challenge cannot be empty.", nameof(challenge));

        return signingKey.SignData(challenge, HashAlgorithmName.SHA256);
    }

    public static bool VerifyChallenge(
        DevicePublicIdentity publicIdentity,
        ReadOnlySpan<byte> challenge,
        ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(publicIdentity);
        if (challenge.IsEmpty || signature.IsEmpty)
            return false;

        using ECDsa verifier = ECDsa.Create();
        try
        {
            verifier.ImportSubjectPublicKeyInfo(publicIdentity.SubjectPublicKeyInfo, out int bytesRead);
            if (bytesRead != publicIdentity.SubjectPublicKeyInfo.Length)
                return false;

            return verifier.VerifyData(challenge, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string DeriveSecurityId(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        if (subjectPublicKeyInfo.IsEmpty)
            throw new ArgumentException("Public identity material cannot be empty.", nameof(subjectPublicKeyInfo));

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(subjectPublicKeyInfo, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        signingKey.Dispose();
        disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
