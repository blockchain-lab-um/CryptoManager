using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Crypto
{
    public sealed record SignDigestRequestDto
    {
        /// <summary>KeyId as a GUID string.</summary>
        [Required]
        public string KeyId { get; init; } = default!;

        /// <summary>Mechanism name, e.g. "RSA_PSS_SHA256".</summary>
        [Required, MinLength(1)]
        public string Mechanism { get; init; } = default!;

        /// <summary>Digest as base64 (MVP: SHA-256 digest length must be 32 bytes).</summary>
        [Required, MinLength(1)]
        public string DigestBase64 { get; init; } = default!;
    }
}
