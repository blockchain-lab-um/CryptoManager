namespace CryptoManager.API.DTOs.Auth;

public sealed class LoginRequestDto
{
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
}