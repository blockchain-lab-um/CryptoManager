namespace CryptoManager.API.DTOs.Certificates;

public sealed record CertificateResponseDto(
    string Id,
    string KeyVersionId,
    string Status,
    string Source,
    string? SerialNumber,
    string? Thumbprint,
    string? SubjectDN,
    string? IssuerDN,
    DateTimeOffset? NotBefore,
    DateTimeOffset? NotAfter,
    string? EnrollmentId,
    DateTimeOffset CreatedAt,
    string CreatedBy);