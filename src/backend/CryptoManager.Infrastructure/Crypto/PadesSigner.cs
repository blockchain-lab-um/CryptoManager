using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Signs PDF files using PAdES via PDFsharp 6.x.
/// TODO (Group 4): replace with HsmBackedPdfSigner that uses DocumentSigningMaterial
/// so private keys never need to leave the HSM.
/// </summary>
public sealed class PadesSigner : IPadesSigner
{
    public Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        string originalFileName,
        byte[] pdfBytes,
        DocumentSigningMaterial material,
        CancellationToken ct)
    {
        // TODO (Group 4): implement using HsmBackedPdfSigner (IDigitalSigner) with
        // material.CertBundle and material.SignDigestAsync callback.
        throw new NotImplementedException("PadesSigner not yet updated. Implement in Group 4.");
    }
}