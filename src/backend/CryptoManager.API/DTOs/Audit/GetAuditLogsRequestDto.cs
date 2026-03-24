namespace CryptoManager.API.DTOs.Audit;

public sealed class GetAuditLogsRequestDto
{
    public string? Actor { get; init; }
    public string? Action { get; init; }
    public string? KeyId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}