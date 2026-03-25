namespace CryptoManager.Application.DTOs;

/// <summary>
/// Parsed metadata extracted from a DER-encoded X.509 certificate.
/// Used by use cases to populate Certificate entity fields without touching
/// X.509 parsing code (which lives in Infrastructure).
/// </summary>
public sealed record CertificateInfo(
    string SerialNumber,
    string Thumbprint,
    string SubjectDN,
    string IssuerDN,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter);