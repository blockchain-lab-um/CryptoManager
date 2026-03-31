namespace CryptoManager.Application.DTOs;

public sealed record VerifySignedFileResult(
    bool IsValid,
    string Format,
    string Message,
    string? SignerName = null,
    string? CertificateSubject = null,
    DateTimeOffset? SigningTime = null
);
