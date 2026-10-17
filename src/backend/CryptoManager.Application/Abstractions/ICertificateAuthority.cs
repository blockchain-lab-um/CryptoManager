using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;

namespace CryptoManager.Application.Abstractions;

/// <summary>
/// Abstracts a Certificate Authority (CA) that can issue and revoke certificates.
/// Implementations are self-contained: all configuration (URLs, credentials, CA key
/// material) is injected at startup via DI. Use cases never know which CA is active.
///
/// Swapping between SoftSelfSignedCertificateAuthority (dev) and a real CA (prod)
/// is done by changing the DI registration only — no use-case or API changes.
/// </summary>
public interface ICertificateAuthority
{
    /// <summary>
    /// Identifies the kind of certificate this CA issues. Used by
    /// SubmitCsrToCAUseCase to stamp CertificateSource on the Certificate entity.
    /// </summary>
    CertificateSource Source { get; }

    /// <summary>
    /// Submits a DER-encoded PKCS#10 CSR to the CA.
    /// Returns an opaque enrollment ID for tracking. The CA may issue the
    /// certificate synchronously (soft CA) or asynchronously (external CA).
    /// </summary>
    Task<string> SubmitCsrAsync(byte[] csrDer, CancellationToken ct = default);

    /// <summary>
    /// Polls the CA for the result of a prior enrollment.
    /// Returns the issued certificate and chain when ready, or null if still pending.
    /// </summary>
    Task<CertificateEnrollmentResult?> PollEnrollmentAsync(string enrollmentId, CancellationToken ct = default);

    /// <summary>
    /// Requests revocation of a certificate identified by its serial number.
    /// Implementations that do not support CRL/OCSP revocation may no-op or log.
    /// </summary>
    Task RevokeCertificateAsync(string serialNumber, RevocationReason reason, CancellationToken ct = default);
}