namespace CryptoManager.Domain.Enums;

public enum AuditAction
{
    CreateKey = 1,
    RotateKey = 2,
    DisableKey = 3,
    DeleteKey = 4,
    Sign = 5,
    GetPublicKey = 6,
    SignFile = 7,
    IssueCertificate = 8,
    ImportCertificate = 9,
    RevokeCertificate = 10,
    ExpireCertificate = 11,
}
