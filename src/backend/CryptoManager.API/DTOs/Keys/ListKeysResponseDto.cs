namespace CryptoManager.API.DTOs.Keys;

public sealed record ListKeysResponseDto
{
    public IReadOnlyList<KeySummaryDto> Keys { get; init; } = [];
}

public sealed record KeySummaryDto
{
    public string KeyId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Purpose { get; init; } = default!;
    public string State { get; init; } = default!;
    public int VersionCount { get; init; }
    public int? PrimaryVersion { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
