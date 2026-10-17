namespace CryptoManager.API.DTOs.Auth;

public sealed class RegisterRequestDto
{
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}