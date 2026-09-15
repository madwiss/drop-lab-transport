using System.Security.Cryptography;
using System.Text;

namespace Drop.Security.Tests;

[TestClass]
public sealed class DeviceIdentityTests
{
    [TestMethod]
    public void GeneratedIdentityContainsPrivateMaterialAndPublicDerivedSecurityId()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();

        byte[] privateMaterial = identity.ExportPrivateIdentity();
        byte[] publicMaterial = identity.PublicIdentity.SubjectPublicKeyInfo;

        Assert.IsTrue(privateMaterial.Length > 0);
        Assert.IsTrue(publicMaterial.Length > 0);
        Assert.AreEqual(DeviceIdentity.DeriveSecurityId(publicMaterial), identity.SecurityId);

        using ECDsa publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(publicMaterial, out int bytesRead);
        Assert.AreEqual(publicMaterial.Length, bytesRead);
        Assert.AreEqual(identity.SecurityId, DeviceIdentity.DeriveSecurityId(publicOnly.ExportSubjectPublicKeyInfo()));
    }

    [TestMethod]
    public void PublicIdentityKeyMaterialCannotBeMutatedByCaller()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        byte[] signature = identity.SignChallenge(challenge);
        string securityId = identity.SecurityId;

        byte[] exposedCopy = identity.PublicIdentity.SubjectPublicKeyInfo;
        exposedCopy[0] ^= 0x01;

        byte[] effectivePublicKey = identity.PublicIdentity.SubjectPublicKeyInfo;
        Assert.AreEqual(securityId, DeviceIdentity.DeriveSecurityId(effectivePublicKey));
        Assert.IsTrue(DeviceIdentity.VerifyChallenge(identity.PublicIdentity, challenge, signature));
        CollectionAssert.AreNotEqual(exposedCopy, effectivePublicKey);
    }
    [TestMethod]
    public async Task PersistenceRoundTripPreservesFingerprintAndSigningIdentity()
    {
        MemoryIdentityStore store = new();
        DeviceIdentityProvider provider = new(store);

        string originalSecurityId;
        byte[] signature;
        byte[] challenge = Encoding.UTF8.GetBytes("drop-device-challenge");

        using (DeviceIdentity original = await provider.LoadOrCreateAsync())
        {
            originalSecurityId = original.SecurityId;
            signature = original.SignChallenge(challenge);
        }

        using DeviceIdentity reloaded = await provider.LoadOrCreateAsync();

        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(originalSecurityId, reloaded.SecurityId);
        Assert.IsTrue(DeviceIdentity.VerifyChallenge(reloaded.PublicIdentity, challenge, signature));
    }

    [TestMethod]
    public void MatchingIdentityVerifiesChallengeSignature()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        byte[] challenge = RandomNumberGenerator.GetBytes(32);

        byte[] signature = identity.SignChallenge(challenge);

        Assert.IsTrue(DeviceIdentity.VerifyChallenge(identity.PublicIdentity, challenge, signature));
    }

    [TestMethod]
    public void ModifiedChallengeFailsVerification()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        byte[] signature = identity.SignChallenge(challenge);
        challenge[0] ^= 0x01;

        Assert.IsFalse(DeviceIdentity.VerifyChallenge(identity.PublicIdentity, challenge, signature));
    }

    [TestMethod]
    public void ModifiedSignatureFailsVerification()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        byte[] signature = identity.SignChallenge(challenge);
        signature[^1] ^= 0x01;

        Assert.IsFalse(DeviceIdentity.VerifyChallenge(identity.PublicIdentity, challenge, signature));
    }

    [TestMethod]
    public void DifferentIdentityFailsVerification()
    {
        using DeviceIdentity signer = DeviceIdentity.Generate();
        using DeviceIdentity otherIdentity = DeviceIdentity.Generate();
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        byte[] signature = signer.SignChallenge(challenge);

        Assert.IsFalse(DeviceIdentity.VerifyChallenge(otherIdentity.PublicIdentity, challenge, signature));
        Assert.AreNotEqual(signer.SecurityId, otherIdentity.SecurityId);
    }

    [TestMethod]
    public void ImportRejectsPrivateIdentityWithTrailingData()
    {
        using DeviceIdentity identity = DeviceIdentity.Generate();
        byte[] privateMaterial = identity.ExportPrivateIdentity();
        byte[] malformed = [.. privateMaterial, 0x00];

        Assert.Throws<CryptographicException>(() => DeviceIdentity.ImportPrivateIdentity(malformed));
    }

    private sealed class MemoryIdentityStore : IDeviceIdentityStore
    {
        private byte[]? persisted;

        public int SaveCount { get; private set; }

        public ValueTask<byte[]?> LoadPrivateIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(persisted is null ? null : (byte[])persisted.Clone());
        }

        public ValueTask SavePrivateIdentityAsync(byte[] privateIdentity, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(privateIdentity);
            persisted = (byte[])privateIdentity.Clone();
            SaveCount++;
            return ValueTask.CompletedTask;
        }
    }
}

