namespace CryptoManager.API.DTOs.Auth;

public sealed class LoginResponseDto
{
    public string Token { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public string UserName { get; set; } = default!;
    public IList<string> Roles { get; set; } = [];
}