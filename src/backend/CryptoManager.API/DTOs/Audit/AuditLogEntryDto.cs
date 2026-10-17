namespace CryptoManager.API.DTOs.Audit;

public sealed class AuditLogEntryDto
{
    public string Id { get; init; } = default!;
    public DateTimeOffset Timestamp { get; init; }
    public string Actor { get; init; } = default!;
    public string? ActorId { get; init; }
    public string Action { get; init; } = default!;
    public string? KeyId { get; init; }
    public string? KeyName { get; init; }
    public int? KeyVersion { get; init; }
    public string? Mechanism { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}