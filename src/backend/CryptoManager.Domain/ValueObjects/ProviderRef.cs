using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

/// <summary>
/// Opaque reference that allows Infrastructure to locate a key in an underlying provider. For example, an object ID in YubiHSM.
/// </summary>
public sealed record ProviderRef
{
    public string ProviderType { get; } // e.g., "PKCS11", "Soft"
    public string ProviderInstanceId { get; set; } // Which named instance holds this key
    public string Reference { get; }    // opaque, provider-specific

    public ProviderRef(string providerType, string reference)
    {
        Guard.NotEmpty(providerType, nameof(providerType));
        Guard.NotEmpty(reference, nameof(reference));
        ProviderType = providerType;
        Reference = reference;
    }
}
