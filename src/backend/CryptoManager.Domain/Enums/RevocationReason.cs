namespace CryptoManager.Domain.Enums;

/// <summary>
/// Maps to RFC 5280 CRLReason codes.
/// </summary>
public enum RevocationReason
{
    Unspecified          = 0,
    KeyCompromise        = 1,
    CACompromise         = 2,
    AffiliationChanged   = 3,
    Superseded           = 4,
    CessationOfOperation = 5,
}