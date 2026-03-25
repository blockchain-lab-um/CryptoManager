namespace CryptoManager.Application.DTOs;

/// <summary>
/// The issued certificate material returned by ICertificateAuthority after a
/// successful enrollment. Both fields are DER-encoded.
/// </summary>
/// <param name="CertDer">The leaf certificate.</param>
/// <param name="ChainDer">
/// Intermediate CA certificates in order (leaf → root), or an empty array for
/// self-signed certs issued by a root-level soft CA.
/// </param>
public record CertificateEnrollmentResult(byte[] CertDer, byte[][] ChainDer);