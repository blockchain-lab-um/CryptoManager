namespace CryptoManager.Domain.Enums;

public enum CertificateStatus
{
    PendingEnrollment = 1,  // CSR submitted, waiting for CA response
    Active            = 2,  // valid, linked, usable for signing
    Expired           = 3,  // past NotAfter — set lazily at signing time
    Revoked           = 4,  // explicitly revoked
    Superseded        = 5,  // replaced by a newer cert on the same KeyVersion
}