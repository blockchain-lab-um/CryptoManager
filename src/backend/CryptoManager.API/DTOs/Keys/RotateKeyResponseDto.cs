namespace CryptoManager.API.DTOs.Keys
{
    public sealed record RotateKeyResponseDto
    {
        public string KeyId { get; init; } = default!;
        public int NewPrimaryVersion { get; init; }
        public string PublicKeyPem { get; init; } = default!;
    }
}
