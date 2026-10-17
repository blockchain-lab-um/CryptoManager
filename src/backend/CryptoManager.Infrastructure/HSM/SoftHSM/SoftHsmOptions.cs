namespace CryptoManager.Infrastructure.HSM.SoftHSM;

public sealed class SoftHsmOptions
{
    /// <summary>
    /// Path to the JSON file where key material is persisted.
    /// If null or empty, keys are kept in memory only (no persistence).
    /// </summary>
    public string? FilePath { get; init; }
}
