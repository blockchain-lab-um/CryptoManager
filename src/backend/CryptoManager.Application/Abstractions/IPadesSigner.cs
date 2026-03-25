using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface IPadesSigner
{
    Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        string originalFileName,
        byte[] pdfBytes,
        DocumentSigningMaterial material,
        CancellationToken ct);
}