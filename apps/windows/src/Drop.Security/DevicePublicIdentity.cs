namespace Drop.Security;

public sealed record DevicePublicIdentity
{
    private readonly byte[] subjectPublicKeyInfo;

    public DevicePublicIdentity(byte[] subjectPublicKeyInfo)
    {
        ArgumentNullException.ThrowIfNull(subjectPublicKeyInfo);
        if (subjectPublicKeyInfo.Length == 0)
            throw new ArgumentException("Public identity material cannot be empty.", nameof(subjectPublicKeyInfo));

        this.subjectPublicKeyInfo = (byte[])subjectPublicKeyInfo.Clone();
        SecurityId = DeviceIdentity.DeriveSecurityId(this.subjectPublicKeyInfo);
    }

    public byte[] SubjectPublicKeyInfo => (byte[])subjectPublicKeyInfo.Clone();

    public string SecurityId { get; }
}
