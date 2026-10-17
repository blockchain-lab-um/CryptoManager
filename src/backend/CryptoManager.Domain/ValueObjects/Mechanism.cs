using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

/// <summary>
/// Mechanism is the fully specified crypto operation: algorithm + scheme/padding + hash + parameters.
/// This is what we whitelist per Key.
/// </summary>
public sealed record Mechanism
{
    public string Name { get; }
    public AlgorithmFamily Family { get; }
    public string HashAlgorithm { get; } // e.g., "SHA256"
    public SignatureEncoding SignatureEncoding { get; }

    private Mechanism(string name, AlgorithmFamily family, string hashAlgorithm, SignatureEncoding signatureEncoding)
    {
        Guard.NotEmpty(name, nameof(name));
        Guard.NotEmpty(hashAlgorithm, nameof(hashAlgorithm));

        Name = name;
        Family = family;
        HashAlgorithm = hashAlgorithm;
        SignatureEncoding = signatureEncoding;
    }

    // Closed set for MVP (extend later)

    public static readonly Mechanism RsaPssSha256 =
        new("RSA_PSS_SHA256", AlgorithmFamily.Rsa, "SHA256", SignatureEncoding.Raw);

    public static readonly Mechanism EcdsaP256Sha256Der =
        new("ECDSA_P256_SHA256", AlgorithmFamily.Ec, "SHA256", SignatureEncoding.Der);

    public static IReadOnlyCollection<Mechanism> All { get; } = new[]
    {
        RsaPssSha256,
        EcdsaP256Sha256Der
    };

    public static Mechanism Parse(string name)
    {
        Guard.NotEmpty(name, nameof(name));
        var mech = All.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (mech is null) throw new DomainException($"Unknown mechanism '{name}'.");
        return mech;
    }
}
