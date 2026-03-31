namespace CryptoManager.API.DTOs.Crypto;

public sealed class VerifySignedFileResponseDto
{
    public bool IsValid { get; init; }
    public string Format { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? SignerName { get; init; }
    public string? CertificateSubject { get; init; }
    public DateTimeOffset? SigningTime { get; init; }
}
