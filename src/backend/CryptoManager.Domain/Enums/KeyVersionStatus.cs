namespace CryptoManager.Domain.Enums;

public enum KeyVersionStatus
{
    Primary = 1,     // Used for new signatures
    Active = 2,      // Valid, but not the default for new signatures
    Retired = 3,     // Kept for verification / historical reasons
    Disabled = 4,    // Compromised / revoked
    Destroyed = 5    // Physically removed from provider
}
