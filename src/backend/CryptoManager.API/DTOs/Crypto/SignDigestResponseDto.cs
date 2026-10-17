namespace CryptoManager.API.DTOs.Crypto
{
    public sealed record SignDigestResponseDto
    {
        public string KeyId { get; init; } = default!;
        public int KeyVersion { get; init; }

        public string Mechanism { get; init; } = default!;
        public string Encoding { get; init; } = default!;

        public string SignatureBase64 { get; init; } = default!;
        public string AuditEventId { get; init; } = default!;
    }
}
