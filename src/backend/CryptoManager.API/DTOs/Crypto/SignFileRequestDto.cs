using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Crypto
{
    public class SignFileRequestDto
    {
        [Required]
        public string KeyId { get; init; } = default!;

        [Required, MinLength(1)]
        public string Mechanism { get; init; } = default!;

        [Required]
        public IFormFile File { get; init; } = default!;

        public bool AddStamp { get; init; } = true;
        public string? StampX { get; init; }
        public string? StampY { get; init; }
        public string? StampWidth { get; init; }
        public string? StampHeight { get; init; }
        public string? StampRotation { get; init; }
    }
}
