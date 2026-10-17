using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record CertificateDto(
    CertificateId Id,
    KeyVersionId KeyVersionId,
    CertificateStatus Status,
    CertificateSource Source,
    string? SerialNumber,
    string? Thumbprint,
    string? SubjectDN,
    string? IssuerDN,
    DateTimeOffset? NotBefore,
    DateTimeOffset? NotAfter,
    string? EnrollmentId,
    DateTimeOffset CreatedAt,
    string CreatedBy);