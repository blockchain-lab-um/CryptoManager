using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;

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
        CancellationToken ct)
    {
        // TODO (Group 4): enforce ECDSA guard, pass material to updated signer implementations.
        throw new NotImplementedException(
            "SignedArtifactBuilder.SignAsync not yet updated for DocumentSigningMaterial. Implement in Group 4.");
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