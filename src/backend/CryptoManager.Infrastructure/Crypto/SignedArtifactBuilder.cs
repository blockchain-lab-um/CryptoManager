using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class SignedArtifactBuilder : ISignedArtifactBuilder
{
    private readonly IPadesSigner _padesSigner;
    private readonly IPkcs7AttachedSigner _pkcs7Signer;

    public SignedArtifactBuilder(IPadesSigner padesSigner, IPkcs7AttachedSigner pkcs7Signer)
    {
        _padesSigner = padesSigner;
        _pkcs7Signer = pkcs7Signer;
    }

    public async Task<SignedArtifact> SignAsync(
        ProviderRef providerKeyRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] fileBytes,
        CancellationToken ct)
    {
        if (LooksLikePdf(fileBytes))
        {
            var signedPdf = await _padesSigner.SignPdfAsync(
                providerKeyRef, mechanism, originalFileName, fileBytes, ct);

            return new SignedArtifact(
                Format: "pades",
                OutputFileName: signedPdf.OutputFileName,
                OutputContentType: "application/pdf",
                Bytes: signedPdf.Bytes);
        }

        var p7m = await _pkcs7Signer.SignAttachedAsync(
            providerKeyRef, mechanism, originalFileName, fileBytes, ct);

        return new SignedArtifact(
            Format: "p7m",
            OutputFileName: p7m.OutputFileName,
            OutputContentType: "application/pkcs7-mime",
            Bytes: p7m.Bytes);
    }

    private static bool LooksLikePdf(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        return bytes[0] == (byte)'%' &&
               bytes[1] == (byte)'P' &&
               bytes[2] == (byte)'D' &&
               bytes[3] == (byte)'F' &&
               bytes[4] == (byte)'-';
    }
}
