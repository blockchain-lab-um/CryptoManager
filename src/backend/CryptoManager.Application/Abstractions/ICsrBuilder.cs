using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.Abstractions;

/// <summary>
/// Builds a DER-encoded PKCS#10 Certificate Signing Request (CSR).
/// The private key never leaves the HSM — signing is delegated via callback.
/// </summary>
public interface ICsrBuilder
{
    /// <summary>
    /// Produces a DER-encoded PKCS#10 CSR.
    /// </summary>
    /// <param name="publicKeyPem">SubjectPublicKeyInfo PEM of the key to certify.</param>
    /// <param name="subject">X.500 subject DN to embed in the CSR.</param>
    /// <param name="signDigestAsync">
    /// Callback that signs a SHA-256 digest using the subject's private key in the HSM.
    /// The use case provides this by closing over IHsmProvider.SignDigestAsync with the
    /// appropriate ProviderRef and Mechanism.
    /// </param>
    /// <returns>DER-encoded CertificationRequest bytes.</returns>
    Task<byte[]> BuildCsrAsync(
        string publicKeyPem,
        SubjectDN subject,
        Func<byte[], Task<byte[]>> signDigestAsync,
        CancellationToken ct = default);
}