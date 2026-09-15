using System.Security.Cryptography;

namespace Drop.Security;

public enum PeerAuthenticationState
{
    Unauthenticated = 0,
    Authenticated = 1,
    Failed = 2
}

public enum AuthenticationFailureKind
{
    UnexpectedPeerFingerprint = 0,
    ChallengeMismatch = 1,
    InvalidProof = 2
}

public sealed record AuthenticationFailure(AuthenticationFailureKind Kind, string Diagnostic);

public sealed record DiscoveryPeerIdentity(
    string? DeviceId = null,
    string? HostName = null,
    string? RouteLabel = null,
    string? EndpointLabel = null);

public sealed record AuthenticatedPeerIdentity
{
    internal AuthenticatedPeerIdentity(DevicePublicIdentity publicIdentity)
    {
        ArgumentNullException.ThrowIfNull(publicIdentity);
        Fingerprint = publicIdentity.SecurityId;
        PublicIdentity = publicIdentity;
    }

    public string Fingerprint { get; }

    public DevicePublicIdentity PublicIdentity { get; }
}

public sealed record PeerSessionIdentity
{
    private PeerSessionIdentity(
        DiscoveryPeerIdentity? discoveryIdentity,
        string? expectedPeerFingerprint,
        AuthenticatedPeerIdentity? authenticatedIdentity,
        AuthenticationFailure? failure)
    {
        DiscoveryIdentity = discoveryIdentity;
        ExpectedPeerFingerprint = expectedPeerFingerprint;
        AuthenticatedIdentity = authenticatedIdentity;
        Failure = failure;
    }

    public DiscoveryPeerIdentity? DiscoveryIdentity { get; }

    public string? ExpectedPeerFingerprint { get; }

    public AuthenticatedPeerIdentity? AuthenticatedIdentity { get; }

    public AuthenticationFailure? Failure { get; }

    public PeerAuthenticationState AuthenticationState => AuthenticatedIdentity is not null
        ? PeerAuthenticationState.Authenticated
        : Failure is not null
            ? PeerAuthenticationState.Failed
            : PeerAuthenticationState.Unauthenticated;

    public static PeerSessionIdentity Unauthenticated(
        DiscoveryPeerIdentity? discoveryIdentity = null,
        string? expectedPeerFingerprint = null)
    {
        if (expectedPeerFingerprint is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerFingerprint);

        return new(discoveryIdentity, expectedPeerFingerprint, null, null);
    }

    public PeerSessionIdentity WithExpectedPeerFingerprint(string expectedPeerFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerFingerprint);
        if (AuthenticationState == PeerAuthenticationState.Authenticated)
            throw new InvalidOperationException("An authenticated session identity cannot change its expected fingerprint.");

        return new(DiscoveryIdentity, expectedPeerFingerprint, null, Failure);
    }

    public PeerSessionIdentity Apply(SessionAuthenticationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.AuthenticatedIdentity is not null &&
            ExpectedPeerFingerprint is not null &&
            !string.Equals(
                ExpectedPeerFingerprint,
                result.AuthenticatedIdentity.Fingerprint,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(
                DiscoveryIdentity,
                ExpectedPeerFingerprint,
                null,
                new AuthenticationFailure(
                    AuthenticationFailureKind.UnexpectedPeerFingerprint,
                    "The authenticated peer does not match the expected fingerprint for this session."));
        }

        return result.AuthenticatedIdentity is not null
            ? new(DiscoveryIdentity, ExpectedPeerFingerprint, result.AuthenticatedIdentity, null)
            : new(DiscoveryIdentity, ExpectedPeerFingerprint, null, result.Failure);
    }
}

public sealed class SessionChallenge
{
    private readonly byte[] nonce;

    public SessionChallenge(Guid challengeId, byte[] nonce)
    {
        if (challengeId == Guid.Empty)
            throw new ArgumentException("Challenge ID cannot be empty.", nameof(challengeId));
        ArgumentNullException.ThrowIfNull(nonce);
        if (nonce.Length < 16)
            throw new ArgumentException("Challenge nonce must contain at least 16 bytes.", nameof(nonce));

        ChallengeId = challengeId;
        this.nonce = (byte[])nonce.Clone();
    }

    public Guid ChallengeId { get; }

    public byte[] Nonce => (byte[])nonce.Clone();
}

public sealed class SessionProof
{
    private readonly byte[] nonce;
    private readonly byte[] signature;

    public SessionProof(Guid challengeId, byte[] nonce, DevicePublicIdentity publicIdentity, byte[] signature)
    {
        if (challengeId == Guid.Empty)
            throw new ArgumentException("Challenge ID cannot be empty.", nameof(challengeId));
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(publicIdentity);
        ArgumentNullException.ThrowIfNull(signature);

        ChallengeId = challengeId;
        this.nonce = (byte[])nonce.Clone();
        PublicIdentity = publicIdentity;
        this.signature = (byte[])signature.Clone();
    }

    public Guid ChallengeId { get; }

    public byte[] Nonce => (byte[])nonce.Clone();

    public DevicePublicIdentity PublicIdentity { get; }

    public byte[] Signature => (byte[])signature.Clone();
}

public sealed record SessionAuthenticationResult
{
    private SessionAuthenticationResult(
        AuthenticatedPeerIdentity? authenticatedIdentity,
        AuthenticationFailure? failure)
    {
        AuthenticatedIdentity = authenticatedIdentity;
        Failure = failure;
    }

    public AuthenticatedPeerIdentity? AuthenticatedIdentity { get; }

    public AuthenticationFailure? Failure { get; }

    public bool IsAuthenticated => AuthenticatedIdentity is not null;

    internal static SessionAuthenticationResult Authenticated(DevicePublicIdentity publicIdentity) =>
        new(new AuthenticatedPeerIdentity(publicIdentity), null);

    public static SessionAuthenticationResult Failed(AuthenticationFailureKind kind, string diagnostic) =>
        new(null, new AuthenticationFailure(kind, diagnostic));
}

public static class SessionAuthenticator
{
    private const int ChallengeSizeBytes = 32;

    public static SessionChallenge CreateChallenge() =>
        new(Guid.NewGuid(), RandomNumberGenerator.GetBytes(ChallengeSizeBytes));

    public static SessionProof CreateProof(DeviceIdentity identity, SessionChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(challenge);

        byte[] payload = BuildSignedPayload(challenge.ChallengeId, challenge.Nonce);
        return new SessionProof(
            challenge.ChallengeId,
            challenge.Nonce,
            identity.PublicIdentity,
            identity.SignChallenge(payload));
    }

    public static SessionAuthenticationResult VerifyProof(
        SessionChallenge expectedChallenge,
        SessionProof proof,
        string? expectedPeerFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(expectedChallenge);
        ArgumentNullException.ThrowIfNull(proof);
        if (expectedPeerFingerprint is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerFingerprint);

        if (expectedPeerFingerprint is not null &&
            !string.Equals(expectedPeerFingerprint, proof.PublicIdentity.SecurityId, StringComparison.OrdinalIgnoreCase))
        {
            return SessionAuthenticationResult.Failed(
                AuthenticationFailureKind.UnexpectedPeerFingerprint,
                "The proof was signed by a peer other than the expected fingerprint.");
        }

        if (proof.ChallengeId != expectedChallenge.ChallengeId ||
            !CryptographicOperations.FixedTimeEquals(proof.Nonce, expectedChallenge.Nonce))
        {
            return SessionAuthenticationResult.Failed(
                AuthenticationFailureKind.ChallengeMismatch,
                "The proof does not match the challenge issued for this session.");
        }

        byte[] payload = BuildSignedPayload(proof.ChallengeId, proof.Nonce);
        if (!DeviceIdentity.VerifyChallenge(proof.PublicIdentity, payload, proof.Signature))
        {
            return SessionAuthenticationResult.Failed(
                AuthenticationFailureKind.InvalidProof,
                "The cryptographic proof is invalid.");
        }

        return SessionAuthenticationResult.Authenticated(proof.PublicIdentity);
    }

    private static byte[] BuildSignedPayload(Guid challengeId, ReadOnlySpan<byte> nonce)
    {
        byte[] challengeIdBytes = challengeId.ToByteArray();
        byte[] payload = new byte[challengeIdBytes.Length + nonce.Length];
        challengeIdBytes.CopyTo(payload, 0);
        nonce.CopyTo(payload.AsSpan(challengeIdBytes.Length));
        return payload;
    }
}
