using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Domain.Entities;

/// <summary>
/// KeyVersion is immutable key material instance (stored in provider/HSM), referenced by ProviderRef.
/// </summary>
public sealed class KeyVersion
{
    public KeyVersionId Id { get; private set; }
    public KeyId KeyId { get; private set; }
    public int Version { get; private set; }

    public KeyVersionStatus Status { get; private set; }
    public ProviderRef ProviderRef { get; private set; } = default!;
    public PublicKeyMaterial PublicKey { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!;

    private KeyVersion() { }

    internal KeyVersion(
        KeyVersionId id,
        KeyId keyId,
        int version,
        KeyVersionStatus status,
        ProviderRef providerRef,
        PublicKeyMaterial publicKey,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Guard.True(id.Value != Guid.Empty, "KeyVersionId must not be empty.");
        Guard.True(keyId.Value != Guid.Empty, "KeyId must not be empty.");
        Guard.True(version > 0, "Version must be positive.");
        Guard.NotNull(providerRef, nameof(providerRef));
        Guard.NotNull(publicKey, nameof(publicKey));
        Guard.NotEmpty(createdBy, nameof(createdBy));

        Id = id;
        KeyId = keyId;
        Version = version;
        Status = status;
        ProviderRef = providerRef;
        PublicKey = publicKey;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    internal void SetStatus(KeyVersionStatus status)
    {
        Status = status;
    }
}
