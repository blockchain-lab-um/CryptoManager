using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Domain.Entities;

/// <summary>
/// Certificate is an independent aggregate root representing an X.509 certificate
/// associated with a specific KeyVersion. Certificate material (DER bytes, serial,
/// thumbprint, subject, validity) is immutable once set. Only lifecycle status
/// and EnrollmentId are mutable.
/// </summary>
public sealed class Certificate
{
    // --- Identity ---
    public CertificateId Id { get; private set; }
    public KeyVersionId KeyVersionId { get; private set; }

    // --- Lifecycle ---
    public CertificateStatus Status { get; private set; }
    public CertificateSource Source { get; private set; }

    /// <summary>CA enrollment tracking reference; populated for PendingEnrollment certs.</summary>
    public string? EnrollmentId { get; private set; }

    // --- Certificate material (immutable once set via Activate) ---
    public string? SerialNumber { get; private set; }
    public string? Thumbprint { get; private set; }   // SHA-256 hex, uppercase
    public string? SubjectDN { get; private set; }
    public string? IssuerDN { get; private set; }
    public DateTimeOffset? NotBefore { get; private set; }
    public DateTimeOffset? NotAfter { get; private set; }
    public byte[]? CertificateDer { get; private set; }
    public byte[][]? ChainDer { get; private set; }   // intermediates in order; null for self-signed with no chain

    // --- Audit ---
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!;

    private Certificate() { } // for EF

    // --- Factory: pending enrollment (CSR submitted, waiting for CA) ---
    public static Certificate CreatePending(
        CertificateId id,
        KeyVersionId keyVersionId,
        CertificateSource source,
        string enrollmentId,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Guard.True(id.Value != Guid.Empty, "CertificateId must not be empty.");
        Guard.True(keyVersionId.Value != Guid.Empty, "KeyVersionId must not be empty.");
        Guard.NotEmpty(enrollmentId, nameof(enrollmentId));
        Guard.NotEmpty(createdBy, nameof(createdBy));

        return new Certificate
        {
            Id           = id,
            KeyVersionId = keyVersionId,
            Status       = CertificateStatus.PendingEnrollment,
            Source       = source,
            EnrollmentId = enrollmentId,
            CreatedAt    = createdAt,
            CreatedBy    = createdBy,
        };
    }

    // --- Factory: directly active (import) ---
    public static Certificate CreateActive(
        CertificateId id,
        KeyVersionId keyVersionId,
        CertificateSource source,
        string serialNumber,
        string thumbprint,
        string subjectDN,
        string issuerDN,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        byte[] certificateDer,
        byte[][]? chainDer,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Guard.True(id.Value != Guid.Empty, "CertificateId must not be empty.");
        Guard.True(keyVersionId.Value != Guid.Empty, "KeyVersionId must not be empty.");
        Guard.NotEmpty(serialNumber, nameof(serialNumber));
        Guard.NotEmpty(thumbprint, nameof(thumbprint));
        Guard.NotEmpty(subjectDN, nameof(subjectDN));
        Guard.NotEmpty(issuerDN, nameof(issuerDN));
        Guard.NotNull(certificateDer, nameof(certificateDer));
        Guard.True(certificateDer.Length > 0, "CertificateDer must not be empty.");
        Guard.NotEmpty(createdBy, nameof(createdBy));

        return new Certificate
        {
            Id             = id,
            KeyVersionId   = keyVersionId,
            Status         = CertificateStatus.Active,
            Source         = source,
            SerialNumber   = serialNumber,
            Thumbprint     = thumbprint.Trim().ToUpperInvariant(),
            SubjectDN      = subjectDN,
            IssuerDN       = issuerDN,
            NotBefore      = notBefore,
            NotAfter       = notAfter,
            CertificateDer = certificateDer,
            ChainDer       = chainDer,
            CreatedAt      = createdAt,
            CreatedBy      = createdBy,
        };
    }

    // --- Transitions ---

    /// <summary>
    /// Completes a pending enrollment by attaching the issued certificate material
    /// and transitioning to Active. Called by CompleteCertificateEnrollmentUseCase.
    /// </summary>
    public void Activate(
        string serialNumber,
        string thumbprint,
        string subjectDN,
        string issuerDN,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        byte[] certificateDer,
        byte[][]? chainDer)
    {
        Guard.True(Status == CertificateStatus.PendingEnrollment,
            $"Only a PendingEnrollment certificate can be activated (current status: {Status}).");
        Guard.NotEmpty(serialNumber, nameof(serialNumber));
        Guard.NotEmpty(thumbprint, nameof(thumbprint));
        Guard.NotEmpty(subjectDN, nameof(subjectDN));
        Guard.NotEmpty(issuerDN, nameof(issuerDN));
        Guard.NotNull(certificateDer, nameof(certificateDer));
        Guard.True(certificateDer.Length > 0, "CertificateDer must not be empty.");

        SerialNumber   = serialNumber;
        Thumbprint     = thumbprint.Trim().ToUpperInvariant();
        SubjectDN      = subjectDN;
        IssuerDN       = issuerDN;
        NotBefore      = notBefore;
        NotAfter       = notAfter;
        CertificateDer = certificateDer;
        ChainDer       = chainDer;
        Status         = CertificateStatus.Active;
    }

    /// <summary>
    /// Transitions to Superseded when a newer certificate becomes Active for the
    /// same KeyVersion. Called within the same transaction as the new cert activation.
    /// </summary>
    public void Supersede()
    {
        Guard.True(Status == CertificateStatus.Active,
            $"Only an Active certificate can be superseded (current status: {Status}).");
        Status = CertificateStatus.Superseded;
    }

    /// <summary>Transitions to Revoked.</summary>
    public void Revoke()
    {
        Guard.True(
            Status is CertificateStatus.Active or CertificateStatus.Superseded,
            $"Cannot revoke a certificate with status {Status}.");
        Status = CertificateStatus.Revoked;
    }

    /// <summary>
    /// Transitions to Expired. Called lazily by SignDocumentUseCase when NotAfter
    /// is found to be in the past. Persisted immediately before the signing attempt
    /// is rejected.
    /// </summary>
    public void MarkExpired()
    {
        Guard.True(Status == CertificateStatus.Active,
            $"Only an Active certificate can be marked expired (current status: {Status}).");
        Status = CertificateStatus.Expired;
    }
}