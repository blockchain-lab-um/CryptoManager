using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Infrastructure.Auditing;

namespace CryptoManager.Infrastructure.Persistence.InMemory;

/// <summary>
/// LINQ-to-objects implementation backed by InMemoryAuditSink.
/// Used when the app runs without a database (no-DB dev scenario).
/// Both IAuditSink and IAuditRepository must be swapped together in DI
/// to keep writes and reads consistent.
/// </summary>
public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly InMemoryAuditSink _sink;
    private readonly InMemoryKeyRepository _keys;

    public InMemoryAuditRepository(InMemoryAuditSink sink, InMemoryKeyRepository keys)
    {
        _sink = sink;
        _keys = keys;
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var q = _sink.Events.AsQueryable();

        if (query.ScopeToActorId is not null)
            q = q.Where(e => e.ActorId == query.ScopeToActorId);

        if (query.Actor is not null)
            q = q.Where(e => e.Actor == query.Actor);

        if (query.Action is not null)
            q = q.Where(e => e.Action == query.Action);

        if (query.KeyId is not null)
            q = q.Where(e => e.KeyId == query.KeyId);

        if (query.From is not null)
            q = q.Where(e => e.Timestamp >= query.From.Value);

        // Matches the EF repository: `To` is end-of-day inclusive.
        if (query.To is not null)
            q = q.Where(e => e.Timestamp < query.To.Value.Date.AddDays(1));

        var totalCount = q.Count();

        var page = q
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var keysById = (await _keys.ListAllAsync())
            .ToDictionary(k => k.Id.Value, k => k.Name);

        var items = page
            .Select(e => new AuditLogEntry(
                e.Id.Value,
                e.Timestamp,
                e.Actor,
                e.ActorId,
                e.Action,
                e.KeyId.HasValue ? e.KeyId.Value.Value : (Guid?)null,
                e.KeyId.HasValue && keysById.TryGetValue(e.KeyId.Value.Value, out var keyName) ? keyName : null,
                e.KeyVersion,
                e.Mechanism,
                e.Success,
                e.Error))
            .ToList();

        return new AuditLogPage(items, query.Page, query.PageSize, totalCount);
    }
}
