using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.Abstractions
{
    public interface IHsmProvider
    {
        Task<(ProviderRef ProviderRef, PublicKeyMaterial PublicKey)> CreateSigningKeyAsync(string KeyName, Mechanism Mechanism);
        Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef providerRef);
        Task<byte[]> SignDigestAsync(ProviderRef providerRef, Mechanism mechanism, byte[] digest);
    }
}
