namespace CryptoManager.API.DTOs.Certificates;

public sealed record RevokeRequestDto
{
    /// <summary>RFC 5280 revocation reason. Defaults to Unspecified (0).</summary>
    public string Reason { get; init; } = "Unspecified";
}