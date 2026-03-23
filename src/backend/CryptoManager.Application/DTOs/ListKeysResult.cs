using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record ListKeysResult(IReadOnlyList<KeySummary> Keys);

public sealed record KeySummary(
    KeyId KeyId,
    string Name,
    KeyPurpose Purpose,
    KeyState State,
    int VersionCount,
    int? PrimaryVersion,
    DateTimeOffset CreatedAt,
    string? Owner
);
