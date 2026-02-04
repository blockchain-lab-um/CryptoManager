namespace CryptoManager.API.DTOs.Keys
{
    public sealed record CreateKeyResponseDto
    {
        public string KeyId { get; init; } = default!;
        public string Name { get; init; } = default!;
        public string Purpose { get; init; } = default!;
        public int PrimaryVersion { get; init; }
        public string PublicKeyPem { get; init; } = default!;
    }
}
