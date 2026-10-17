namespace CryptoManager.API.DTOs.Certificates;

public sealed record CompleteEnrollmentResponseDto(
    bool IsComplete,
    string? CertificateId = null,
    string? Thumbprint = null);