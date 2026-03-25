namespace CryptoManager.Application.DTOs;

/// <summary>
/// DER-encoded certificate material passed from Application to Infrastructure.
/// Application layer works exclusively with raw bytes; Infrastructure materialises
/// X509Certificate2 / BouncyCastle certificate objects internally.
/// </summary>
/// <param name="LeafDer">DER-encoded leaf certificate (no private key).</param>
/// <param name="ChainDer">DER-encoded intermediate CA certificates in order, or empty.</param>
public record RawCertBundle(byte[] LeafDer, byte[][] ChainDer)
{
    public static readonly byte[][] EmptyChain = [];
}