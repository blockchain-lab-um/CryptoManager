using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.DTOs;

/// <summary>
/// Internal query object passed from the use case to IAuditRepository.
/// ScopeToActorId is set by the use case (never by the API caller) and is the
/// authorization boundary: non-admin users can only see their own rows.
/// </summary>
public sealed record AuditLogQuery(
    /// <summary>
    /// When non-null, restricts results to events whose ActorId matches this value.
    /// Null means the caller is an admin and all rows are visible.
    /// </summary>
    string? ScopeToActorId,
    /// <summary>
    /// Admin-only filter: narrow results to a specific actor username.
    /// Silently ignored when ScopeToActorId is set (i.e. for non-admin callers).
    /// This is intentional — the UI does not send the field for regular users,
    /// but the use case strips it regardless so the contract is enforced server-side.
    /// </summary>
    string? Actor,
    AuditAction? Action,
    KeyId? KeyId,
    DateTimeOffset? From,
    /// <summary>
    /// Inclusive end of the date range. The repository treats this as end-of-day
    /// (i.e. applies &lt; To.Date.AddDays(1)) so callers can pass a date-only value.
    /// </summary>
    DateTimeOffset? To,
    int Page,
    int PageSize);
