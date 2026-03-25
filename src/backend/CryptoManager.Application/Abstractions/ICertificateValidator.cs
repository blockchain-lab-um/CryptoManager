using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

/// <summary>
/// Validates certificate material against key material and chain trust.
/// Used during certificate import and enrollment completion.
/// </summary>
public interface ICertificateValidator
{
    /// <summary>
    /// Verifies that the public key embedded in the certificate matches the
    /// provided SubjectPublicKeyInfo PEM. Returns false on mismatch.
    /// </summary>
    bool VerifyMatchesPublicKey(byte[] certDer, string publicKeyPem);

    /// <summary>
    /// Validates the certificate chain at the specified point in time.
    /// <paramref name="chainDer"/> should contain intermediate CA certificates
    /// in order from the leaf's issuer toward the root.
    /// </summary>
    ChainValidationResult ValidateChain(byte[] certDer, byte[][] chainDer, DateTimeOffset at);

    /// <summary>
    /// Extracts metadata fields from a DER-encoded certificate.
    /// Keeps X.509 parsing in Infrastructure; use cases receive a plain DTO.
    /// </summary>
    CertificateInfo ParseCertificateInfo(byte[] certDer);
}