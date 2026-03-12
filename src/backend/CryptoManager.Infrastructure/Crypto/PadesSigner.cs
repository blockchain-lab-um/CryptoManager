using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.ValueObjects;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Signatures;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Signs PDF files using PAdES (PDF Advanced Electronic Signatures) via PDFsharp 6.x.
/// Uses PdfSharpDefaultSigner which signs using the certificate's private key via .NET's
/// System.Security.Cryptography.Pkcs.SignedCms. For SoftHsmProvider the self-signed certificate
/// returned by GetSigningCertificateAsync includes the in-memory private key, so this works
/// directly. For a real HSM, provide a certificate whose private key is backed by a custom
/// CryptoServiceProvider (e.g. via X509Certificate2.CopyWithPrivateKey with a custom RSA wrapper).
/// </summary>
public sealed class PadesSigner : IPadesSigner
{
    private readonly IHsmProviderRegistry _hsmRegistry;

    public PadesSigner(IHsmProviderRegistry hsmRegistry)
    {
        _hsmRegistry = hsmRegistry;
    }

    public async Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        ProviderRef providerRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] pdfBytes,
        CancellationToken ct)
    {
        // The certificate from SoftHsmProvider includes the private key (CertificateRequest.CreateSelfSigned).
        // PdfSharpDefaultSigner uses it via the .NET SignedCms class.
        var provider = _hsmRegistry.Resolve(providerRef.ProviderInstanceId);
        var cert = await provider.GetSigningCertificateAsync(providerRef);

        using var inputStream = new MemoryStream(pdfBytes);
        var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

        var signer = new PdfSharpDefaultSigner(cert, PdfMessageDigestType.SHA256, null);
        var options = new DigitalSignatureOptions();

        // ForDocument registers the handler with the document.
        // The handler intercepts Save to write the signature placeholder,
        // compute byte ranges, call signer.GetSignatureAsync, and patch the result.
        DigitalSignatureHandler.ForDocument(document, signer, options);

        using var outputStream = new MemoryStream();
        document.Save(outputStream);

        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        return (baseName + ".signed.pdf", outputStream.ToArray());
    }
}
