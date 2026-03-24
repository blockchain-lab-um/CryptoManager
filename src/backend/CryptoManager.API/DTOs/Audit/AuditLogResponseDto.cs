namespace CryptoManager.API.DTOs.Audit;

public sealed class AuditLogResponseDto
{
    public List<AuditLogEntryDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}