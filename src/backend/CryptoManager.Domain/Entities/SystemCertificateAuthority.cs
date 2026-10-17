using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Domain.Entities;

public sealed class SystemCertificateAuthority
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public ProviderRef ProviderRef { get; private set; } = default!;
    public byte[] CertificateDer { get; private set; } = default!;
    public string SubjectDn { get; private set; } = default!;
    public string Thumbprint { get; private set; } = default!;
    public string SerialNumber { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; }

    private SystemCertificateAuthority() { }

    public static SystemCertificateAuthority Create(
        string name,
        ProviderRef providerRef,
        byte[] certificateDer,
        string subjectDn,
        string thumbprint,
        string serialNumber,
        DateTimeOffset createdAt)
    {
        Guard.NotEmpty(name, nameof(name));
        Guard.NotNull(providerRef, nameof(providerRef));
        Guard.NotNull(certificateDer, nameof(certificateDer));
        Guard.True(certificateDer.Length > 0, "CertificateDer must not be empty.");
        Guard.NotEmpty(subjectDn, nameof(subjectDn));
        Guard.NotEmpty(thumbprint, nameof(thumbprint));
        Guard.NotEmpty(serialNumber, nameof(serialNumber));

        return new SystemCertificateAuthority
        {
            Id = Guid.NewGuid(),
            Name = name,
            ProviderRef = providerRef,
            CertificateDer = certificateDer,
            SubjectDn = subjectDn,
            Thumbprint = thumbprint.Trim().ToUpperInvariant(),
            SerialNumber = serialNumber,
            CreatedAt = createdAt,
            IsActive = true
        };
    }
}
