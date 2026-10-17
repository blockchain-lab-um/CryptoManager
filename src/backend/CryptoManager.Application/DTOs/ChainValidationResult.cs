namespace CryptoManager.Application.DTOs;

/// <param name="IsValid">True if the chain is trusted and all certs are valid at the checked time.</param>
/// <param name="Error">Human-readable failure reason when IsValid is false; null otherwise.</param>
public record ChainValidationResult(bool IsValid, string? Error = null)
{
    public static ChainValidationResult Ok() => new(true);
    public static ChainValidationResult Fail(string error) => new(false, error);
}