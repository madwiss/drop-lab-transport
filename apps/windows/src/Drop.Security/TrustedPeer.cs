namespace Drop.Security;

public sealed record TrustedPeer(
    string DeviceId,
    string Fingerprint,
    string DisplayName,
    bool IsTrusted,
    DateTimeOffset UpdatedAtUtc);
