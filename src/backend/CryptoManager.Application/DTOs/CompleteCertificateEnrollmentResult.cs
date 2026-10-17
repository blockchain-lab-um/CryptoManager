using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record CompleteCertificateEnrollmentResult(
    bool IsComplete,
    CertificateId? CertificateId = null,
    string? Thumbprint = null);