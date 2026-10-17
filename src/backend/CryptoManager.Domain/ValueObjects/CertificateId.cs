namespace CryptoManager.Domain.ValueObjects;

public readonly record struct CertificateId(Guid Value)
{
    public static CertificateId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}