using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.Abstractions;

public interface ISignedArtifactBuilder
{
    Task<SignedArtifact> SignAsync(
        ProviderRef providerKeyRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] fileBytes,
        CancellationToken ct);
}
