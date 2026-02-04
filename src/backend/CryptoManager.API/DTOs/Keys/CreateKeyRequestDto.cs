using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Keys
{
    public sealed record CreateKeyRequestDto
    {
        /// <summary>Human-friendly unique key name.</summary>
        [Required, MinLength(1)]
        public string Name { get; init; } = default!;

        /// <summary>Key purpose (MVP: "Sign").</summary>
        [Required]
        public string Purpose { get; init; } = "Sign";

        /// <summary>Allowed mechanisms for this key, e.g. ["RSA_PSS_SHA256"]</summary>
        [Required, MinLength(1)]
        public string[] AllowedMechanisms { get; init; } = Array.Empty<string>();
    }
}
