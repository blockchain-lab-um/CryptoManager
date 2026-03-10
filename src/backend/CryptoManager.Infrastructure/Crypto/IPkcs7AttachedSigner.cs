using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Infrastructure.Crypto;

public interface IPkcs7AttachedSigner
{
    Task<(string OutputFileName, byte[] Bytes)> SignAttachedAsync(
        ProviderRef providerRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] fileBytes,
        CancellationToken ct);
}
