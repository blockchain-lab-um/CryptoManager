using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record ImportCertificateResult(CertificateId CertificateId, string Thumbprint);