namespace CryptoManager.Domain.Enums;

public enum AuditAction
{
    CreateKey = 1,
    RotateKey = 2,
    DisableKey = 3,
    DeleteKey = 4,
    Sign = 5,
    GetPublicKey = 6
}
