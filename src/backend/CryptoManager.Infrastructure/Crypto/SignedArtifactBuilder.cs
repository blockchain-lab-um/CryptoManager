using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Exceptions;
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
        string originalFileName,
        byte[] fileBytes,
        DocumentSigningMaterial material,
        StampOptions? stamp,
        CancellationToken ct)
    {
        if (material.Mechanism == Mechanism.EcdsaP256Sha256Der)
            throw new DomainException("ECDSA document signing is not yet supported. Use RSA_PSS_SHA256.");

        if (LooksLikePdf(fileBytes))
        {
            var (outputFileName, bytes) = await _padesSigner.SignPdfAsync(
                originalFileName, fileBytes, material, stamp, ct);
            return new SignedArtifact("PAdES", outputFileName, "application/pdf", bytes);
        }
        else
        {
            var (outputFileName, bytes) = await _pkcs7Signer.SignAttachedAsync(
                originalFileName, fileBytes, material, ct);
            return new SignedArtifact("PKCS7-Attached", outputFileName, "application/pkcs7-mime", bytes);
        }
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