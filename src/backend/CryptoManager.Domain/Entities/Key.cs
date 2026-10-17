using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Domain.Entities;

/// <summary>
/// Key is the stable logical identity: name/purpose/policy.
/// It has one or more KeyVersions that map to actual key material in an HSM.
/// </summary>
public sealed class Key
{
    public KeyId Id { get; private set; }
    public string Name { get; private set; } = default!;
    public KeyPurpose Purpose { get; private set; }
    public KeyState State { get; private set; }

    // Whitelist of mechanisms permitted for this Key.
    private readonly HashSet<string> _allowedMechanismNames = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> AllowedMechanisms => _allowedMechanismNames;

    private readonly List<KeyVersion> _versions = new();
    public IReadOnlyList<KeyVersion> Versions => _versions;

    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!; // actor username

    /// <summary>Identity subject (user ID) of the user who owns this key.</summary>
    public string OwnerId { get; private set; } = default!;

    private Key() { } // for ORM

    public Key(KeyId id, string name, KeyPurpose purpose, IEnumerable<Mechanism> allowedMechanisms, DateTimeOffset createdAt, string createdBy, string ownerId)
    {
        Guard.True(id.Value != Guid.Empty, "KeyId must not be empty.");
        Guard.NotEmpty(name, nameof(name));
        Guard.NotNull(allowedMechanisms, nameof(allowedMechanisms));
        Guard.NotEmpty(createdBy, nameof(createdBy));
        Guard.NotEmpty(ownerId, nameof(ownerId));

        Id = id;
        Name = name;
        Purpose = purpose;
        State = KeyState.Active;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        OwnerId = ownerId;

        foreach (var mech in allowedMechanisms)
            _allowedMechanismNames.Add(mech.Name);

        Guard.True(_allowedMechanismNames.Count > 0, "At least one allowed mechanism must be configured.");
    }

    /// <summary>Returns true if the given userId is the owner of this key.</summary>
    public bool IsOwnedBy(string userId) =>
        string.Equals(OwnerId, userId, StringComparison.Ordinal);

    public bool IsMechanismAllowed(Mechanism mechanism) =>
        _allowedMechanismNames.Contains(mechanism.Name);

    public KeyVersion GetPrimaryVersion()
    {
        Guard.True(State == KeyState.Active, $"Key '{Name}' is not active.");
        var primary = _versions.FirstOrDefault(v => v.Status == KeyVersionStatus.Primary);
        if (primary is null) throw new DomainException($"Key '{Name}' has no primary version.");
        return primary;
    }

    public void Disable()
    {
        Guard.True(State == KeyState.Active, "Only active keys can be disabled.");
        State = KeyState.Disabled;
    }

    public void Delete()
    {
        Guard.True(State != KeyState.Deleted, "Key is already deleted.");
        State = KeyState.Deleted;
    }

    /// <summary>
    /// Adds a new version. The first version becomes PRIMARY automatically.
    /// Later versions are created Active and you call PromoteVersionToPrimary() explicitly.
    /// </summary>
    public KeyVersion AddVersion(KeyVersionId versionId, int versionNumber, ProviderRef providerRef, PublicKeyMaterial publicKey, DateTimeOffset createdAt, string createdBy)
    {
        Guard.True(State == KeyState.Active, "Cannot add version to a non-active key.");
        Guard.True(versionNumber > 0, "Version number must be positive.");
        Guard.NotNull(providerRef, nameof(providerRef));
        Guard.NotNull(publicKey, nameof(publicKey));
        Guard.NotEmpty(createdBy, nameof(createdBy));

        Guard.True(_versions.All(v => v.Version != versionNumber), $"Version {versionNumber} already exists.");

        var status = _versions.Count == 0 ? KeyVersionStatus.Primary : KeyVersionStatus.Active;

        var kv = new KeyVersion(
            versionId,
            Id,
            versionNumber,
            status,
            providerRef,
            publicKey,
            createdAt,
            createdBy
        );

        _versions.Add(kv);
        return kv;
    }

    public void PromoteVersionToPrimary(int versionNumber)
    {
        Guard.True(State == KeyState.Active, "Cannot promote version for a non-active key.");

        var target = _versions.FirstOrDefault(v => v.Version == versionNumber)
            ?? throw new DomainException($"Version {versionNumber} not found.");

        Guard.True(target.Status is not KeyVersionStatus.Disabled and not KeyVersionStatus.Destroyed,
            "Cannot promote a disabled/destroyed key version.");

        foreach (var v in _versions)
        {
            if (v.Status == KeyVersionStatus.Primary)
                v.SetStatus(KeyVersionStatus.Active);
        }

        target.SetStatus(KeyVersionStatus.Primary);
    }

    public void RetireVersion(int versionNumber)
    {
        var v = _versions.FirstOrDefault(x => x.Version == versionNumber)
            ?? throw new DomainException($"Version {versionNumber} not found.");

        Guard.True(v.Status != KeyVersionStatus.Primary, "Cannot retire the primary version. Promote another version first.");
        v.SetStatus(KeyVersionStatus.Retired);
    }
}
