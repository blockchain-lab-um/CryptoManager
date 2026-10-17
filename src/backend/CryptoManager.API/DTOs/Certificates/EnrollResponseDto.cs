namespace CryptoManager.API.DTOs.Certificates;

public sealed record EnrollResponseDto(
    string CertificateId,
    string EnrollmentId);