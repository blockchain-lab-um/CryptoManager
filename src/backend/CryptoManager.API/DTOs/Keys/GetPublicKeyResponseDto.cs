namespace CryptoManager.API.DTOs.Keys
{
    public sealed record GetPublicKeyResponseDto
    {
        public string KeyId { get; init; } = default!;
        public int KeyVersion { get; init; }
        public string PublicKeyPem { get; init; } = default!;
    }
}
