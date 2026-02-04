namespace CryptoManager.Domain.Enums;

/// <summary>
/// The encoding format of a digital signature.
/// </summary>
public enum SignatureEncoding
{
    /// <summary>
    /// Raw signature bytes.
    /// </summary>
    Raw = 1,

    /// <summary>
    /// ASN.1 DER encoding.
    /// </summary>
    Der = 2
}
