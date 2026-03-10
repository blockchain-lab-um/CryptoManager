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
    }
}
