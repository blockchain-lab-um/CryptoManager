using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

public sealed record PublicKeyMaterial
{
    /// <summary>
    /// Public key in PEM (SubjectPublicKeyInfo) or certificate PEM.
    /// </summary>
    public string Pem { get; }

    public PublicKeyMaterial(string pem)
    {
        Guard.NotEmpty(pem, nameof(pem));
        Pem = pem;
    }
}
