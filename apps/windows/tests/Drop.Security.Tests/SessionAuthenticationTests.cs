namespace Drop.Security.Tests;

[TestClass]
public sealed class SessionAuthenticationTests
{
    [TestMethod]
    public void DiscoveryMetadataAloneNeverAuthenticatesPeer()
    {
        DiscoveryPeerIdentity discovery = new(
            DeviceId: "same-device-id",
            HostName: "drop-pc.local",
            RouteLabel: "LAN",
            EndpointLabel: "192.168.1.20:53317");

        PeerSessionIdentity session = PeerSessionIdentity.Unauthenticated(discovery);

        Assert.AreEqual(PeerAuthenticationState.Unauthenticated, session.AuthenticationState);
        Assert.IsNull(session.AuthenticatedIdentity);
        Assert.AreEqual("same-device-id", session.DiscoveryIdentity!.DeviceId);
    }

    [TestMethod]
    public void SuccessfulProofAuthenticatesVerifiedPublicFingerprint()
    {
        using DeviceIdentity peer = DeviceIdentity.Generate();
        SessionChallenge challenge = SessionAuthenticator.CreateChallenge();
        SessionProof proof = SessionAuthenticator.CreateProof(peer, challenge);

        SessionAuthenticationResult result = SessionAuthenticator.VerifyProof(
            challenge,
            proof,
            expectedPeerFingerprint: peer.SecurityId);

        Assert.IsTrue(result.IsAuthenticated);
        Assert.IsNull(result.Failure);
        Assert.AreEqual(peer.SecurityId, result.AuthenticatedIdentity!.Fingerprint);
    }

    [TestMethod]
    public void WrongKeyProducesTypedAuthenticationFailure()
    {
        using DeviceIdentity expectedPeer = DeviceIdentity.Generate();
        using DeviceIdentity wrongPeer = DeviceIdentity.Generate();
        SessionChallenge challenge = SessionAuthenticator.CreateChallenge();
        SessionProof proof = SessionAuthenticator.CreateProof(wrongPeer, challenge);

        SessionAuthenticationResult result = SessionAuthenticator.VerifyProof(
            challenge,
            proof,
            expectedPeer.SecurityId);

        Assert.IsFalse(result.IsAuthenticated);
        Assert.AreEqual(AuthenticationFailureKind.UnexpectedPeerFingerprint, result.Failure!.Kind);
    }

    [TestMethod]
    public void SessionExpectedFingerprintRejectsAuthenticatedResultForDifferentPeer()
    {
        using DeviceIdentity expectedPeer = DeviceIdentity.Generate();
        using DeviceIdentity differentPeer = DeviceIdentity.Generate();
        SessionChallenge challenge = SessionAuthenticator.CreateChallenge();
        SessionProof proof = SessionAuthenticator.CreateProof(differentPeer, challenge);
        SessionAuthenticationResult verifiedWithoutExpectation = SessionAuthenticator.VerifyProof(challenge, proof);
        PeerSessionIdentity session = PeerSessionIdentity.Unauthenticated(
            expectedPeerFingerprint: expectedPeer.SecurityId);

        PeerSessionIdentity applied = session.Apply(verifiedWithoutExpectation);

        Assert.AreEqual(PeerAuthenticationState.Failed, applied.AuthenticationState);
        Assert.IsNull(applied.AuthenticatedIdentity);
        Assert.AreEqual(AuthenticationFailureKind.UnexpectedPeerFingerprint, applied.Failure!.Kind);
        Assert.IsTrue(string.Equals(
            expectedPeer.SecurityId,
            applied.ExpectedPeerFingerprint,
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void AuthenticatedResultsCannotBeConstructedByCallers()
    {
        Assert.IsEmpty(typeof(AuthenticatedPeerIdentity).GetConstructors());
        Assert.IsEmpty(typeof(SessionAuthenticationResult).GetConstructors());

        SessionAuthenticationResult manualFailure = SessionAuthenticationResult.Failed(
            AuthenticationFailureKind.InvalidProof,
            "manual failure");
        PeerSessionIdentity applied = PeerSessionIdentity.Unauthenticated().Apply(manualFailure);

        Assert.AreEqual(PeerAuthenticationState.Failed, applied.AuthenticationState);
        Assert.IsNull(applied.AuthenticatedIdentity);
    }

    [TestMethod]
    public void TamperedChallengeProducesTypedAuthenticationFailure()
    {
        using DeviceIdentity peer = DeviceIdentity.Generate();
        SessionChallenge originalChallenge = SessionAuthenticator.CreateChallenge();
        SessionProof proof = SessionAuthenticator.CreateProof(peer, originalChallenge);
        byte[] tamperedNonce = originalChallenge.Nonce;
        tamperedNonce[0] ^= 0x01;
        SessionChallenge tamperedChallenge = new(originalChallenge.ChallengeId, tamperedNonce);

        SessionAuthenticationResult result = SessionAuthenticator.VerifyProof(tamperedChallenge, proof);

        Assert.IsFalse(result.IsAuthenticated);
        Assert.AreEqual(AuthenticationFailureKind.ChallengeMismatch, result.Failure!.Kind);
    }

    [TestMethod]
    public void TamperedProofProducesTypedAuthenticationFailure()
    {
        using DeviceIdentity peer = DeviceIdentity.Generate();
        SessionChallenge challenge = SessionAuthenticator.CreateChallenge();
        SessionProof validProof = SessionAuthenticator.CreateProof(peer, challenge);
        byte[] tamperedSignature = validProof.Signature;
        tamperedSignature[^1] ^= 0x01;
        SessionProof tamperedProof = new(
            validProof.ChallengeId,
            validProof.Nonce,
            validProof.PublicIdentity,
            tamperedSignature);

        SessionAuthenticationResult result = SessionAuthenticator.VerifyProof(challenge, tamperedProof);

        Assert.IsFalse(result.IsAuthenticated);
        Assert.AreEqual(AuthenticationFailureKind.InvalidProof, result.Failure!.Kind);
    }
}
