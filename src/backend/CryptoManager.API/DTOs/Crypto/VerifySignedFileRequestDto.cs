using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Crypto;

public sealed class VerifySignedFileRequestDto
{
    [Required]
    public IFormFile File { get; init; } = default!;
}
