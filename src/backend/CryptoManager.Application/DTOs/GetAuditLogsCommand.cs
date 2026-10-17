using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

public sealed record GetAuditLogsCommand(
    string? Actor,
    AuditAction? Action,
    KeyId? KeyId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 25);
