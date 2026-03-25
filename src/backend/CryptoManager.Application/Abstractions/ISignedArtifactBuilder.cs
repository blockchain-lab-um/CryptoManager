using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface ISignedArtifactBuilder
{
    Task<SignedArtifact> SignAsync(
        string originalFileName,
        byte[] fileBytes,
        DocumentSigningMaterial material,
        CancellationToken ct);
}
