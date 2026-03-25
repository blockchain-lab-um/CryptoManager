using System.ComponentModel.DataAnnotations;

namespace CryptoManager.API.DTOs.Certificates;

public sealed record ImportCertificateRequestDto
{
    /// <summary>DER-encoded leaf certificate, base64-encoded.</summary>
    [Required, MinLength(1)]
    public string CertificateDerBase64 { get; init; } = default!;

    /// <summary>
    /// Ordered chain of intermediate CA certificates (DER, base64), closest to leaf first.
    /// Omit or leave empty for self-signed or when the CA is already trusted.
    /// </summary>
    public string[]? ChainDerBase64 { get; init; }
}