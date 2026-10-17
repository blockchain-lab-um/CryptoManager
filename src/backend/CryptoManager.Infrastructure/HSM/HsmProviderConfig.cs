using CryptoManager.Infrastructure.HSM.PKCS11;
using CryptoManager.Infrastructure.HSM.SoftHSM;

namespace CryptoManager.Infrastructure.HSM;

public sealed class HsmProviderConfig
{
    public string Id { get; init; } = default!;
    public string Type { get; init; } = default!;
    public bool IsDefault { get; init; }
    public Pkcs11Options? Pkcs11 { get; init; }
    public SoftHsmOptions? SoftHsm { get; init; }
}