using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.Abstractions;

public interface IHsmProvider
{
    string InstanceId { get; }
    bool IsAvailable();
    Task<(ProviderRef ProviderRef, PublicKeyMaterial PublicKey)> CreateSigningKeyAsync(string KeyName, Mechanism Mechanism);
    Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef providerRef);
    Task<byte[]> SignDigestAsync(ProviderRef providerRef, Mechanism mechanism, byte[] digest);
    Task DestroyPrivateKeyAsync(ProviderRef providerRef);
}
