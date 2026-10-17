using CryptoManager.Domain.Enums;

namespace CryptoManager.Application.DTOs;

public sealed record AuditLogPage(
    IReadOnlyList<AuditLogEntry> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AuditLogEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string Actor,
    string? ActorId,
    AuditAction Action,
    Guid? KeyId,
    string? KeyName,
    int? KeyVersion,
    string? Mechanism,
    bool Success,
    /// <summary>
    /// Raw error message. Nulled out by the use case for non-admin callers.
    /// </summary>
    string? Error);
