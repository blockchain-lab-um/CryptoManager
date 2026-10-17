using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record SoftCaMaterial(
    ProviderRef ProviderRef,
    byte[] CertificateDer,
    string SubjectDn,
    string Thumbprint,
    string SerialNumber);
