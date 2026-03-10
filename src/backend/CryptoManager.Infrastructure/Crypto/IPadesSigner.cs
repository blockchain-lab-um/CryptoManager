using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Infrastructure.Crypto;

public interface IPadesSigner
{
    Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        ProviderRef providerKeyRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] pdfBytes,
        CancellationToken ct);
}
