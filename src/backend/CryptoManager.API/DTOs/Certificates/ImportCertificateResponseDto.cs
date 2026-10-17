namespace CryptoManager.API.DTOs.Certificates;

public sealed record ImportCertificateResponseDto(
    string CertificateId,
    string Thumbprint);