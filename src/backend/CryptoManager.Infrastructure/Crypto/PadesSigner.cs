using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Signatures;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Signs PDF files using PAdES via PDFsharp 6.x, delegating the actual signing
/// operation to an HSM callback so private keys never leave the HSM.
/// </summary>
public sealed class PadesSigner : IPadesSigner
{
    public async Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        string originalFileName,
        byte[] pdfBytes,
        DocumentSigningMaterial material,
        CancellationToken ct)
    {
        if (material.Mechanism == Mechanism.EcdsaP256Sha256Der)
            throw new NotSupportedException("ECDSA document signing is not yet supported. Use RSA_PSS_SHA256.");

        var signer = new HsmBackedPdfSigner(
            material.CertBundle.LeafDer,
            material.CertBundle.ChainDer,
            material.SignDigestAsync);

        byte[] signed;
        using (var inputStream = new MemoryStream(pdfBytes))
        using (var outputStream = new MemoryStream())
        {
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            DigitalSignatureHandler.ForDocument(document, signer, new DigitalSignatureOptions());
            document.Save(outputStream, closeStream: false);
            signed = outputStream.ToArray();
        }

        var outputFileName = Path.GetFileNameWithoutExtension(originalFileName) + "_signed.pdf";
        return await Task.FromResult((outputFileName, signed));
    }
}