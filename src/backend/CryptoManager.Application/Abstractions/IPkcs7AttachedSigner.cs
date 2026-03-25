using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface IPkcs7AttachedSigner
{
    Task<(string OutputFileName, byte[] Bytes)> SignAttachedAsync(
        string originalFileName,
        byte[] fileBytes,
        DocumentSigningMaterial material,
        CancellationToken ct);
}