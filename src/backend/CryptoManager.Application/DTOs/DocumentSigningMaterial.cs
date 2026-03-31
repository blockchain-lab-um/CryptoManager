using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

/// <summary>
/// Everything Infrastructure needs to sign a document on behalf of a use case.
/// The Application layer constructs this before calling ISignedArtifactBuilder;
/// Infrastructure never reads from IHsmProvider or ICertificateRepository itself.
/// </summary>
/// <param name="Mechanism">
/// Signing mechanism, used by Infrastructure to select the correct CMS algorithm
/// identifier (e.g. RSA-PSS vs ECDSA) without inspecting the callback.
/// </param>
/// <param name="CertBundle">
/// DER-encoded certificate and chain loaded from the database.
/// </param>
/// <param name="SignDigestAsync">
/// Callback that signs a SHA-256 digest using the subject's private key in the HSM.
/// Provided by the use case as a closure over IHsmProvider.SignDigestAsync with the
/// appropriate ProviderRef and Mechanism already captured.
/// </param>
public record DocumentSigningMaterial(
    Mechanism Mechanism,
    RawCertBundle CertBundle,
    Func<byte[], Task<byte[]>> SignDigestAsync,
    string RequestedBy,
    DateTimeOffset SignedAt);
