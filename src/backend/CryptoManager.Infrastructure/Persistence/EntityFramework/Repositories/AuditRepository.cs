using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;

    public AuditRepository(AppDbContext db) => _db = db;

    public async Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var q = _db.AuditEvents.AsNoTracking();

        // Authorization scope — applied first as the outermost filter.
        // Null means admin (all rows visible); non-null restricts to that actor's rows only.
        if (query.ScopeToActorId is not null)
            q = q.Where(e => e.ActorId == query.ScopeToActorId);

        // Admin-only actor filter (by username).
        if (query.Actor is not null)
            q = q.Where(e => e.Actor == query.Actor);

        if (query.Action is not null)
            q = q.Where(e => e.Action == query.Action);

        if (query.KeyId is not null)
            q = q.Where(e => e.KeyId == query.KeyId);

        if (query.From is not null)
            q = q.Where(e => e.Timestamp >= query.From.Value);

        // `To` is treated as end-of-day inclusive: the caller passes a date-only value and
        // we apply < start-of-next-day so the full last day is included.
        if (query.To is not null)
            q = q.Where(e => e.Timestamp < query.To.Value.Date.AddDays(1));

        var totalCount = await q.CountAsync(ct);

        // Fetch the page without a join — EF can't translate the nullable KeyId join.
        var pageRows = await q
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new
            {
                e.Id,
                e.Timestamp,
                e.Actor,
                e.ActorId,
                e.Action,
                KeyIdGuid = e.KeyId.HasValue ? e.KeyId.Value.Value : (Guid?)null,
                e.KeyVersion,
                e.Mechanism,
                e.Success,
                e.Error
            })
            .ToListAsync(ct);

        // Resolve key names for the distinct KeyIds on this page in a single round-trip.
        // Use List<KeyId> (not List<Guid>) so EF can translate Contains via the value converter.
        var keyIds = pageRows
            .Where(r => r.KeyIdGuid.HasValue)
            .Select(r => new KeyId(r.KeyIdGuid!.Value))
            .Distinct()
            .ToList();

        Dictionary<Guid, string> keyNames = [];
        if (keyIds.Count > 0)
        {
            keyNames = await _db.Keys.AsNoTracking()
                .Where(k => keyIds.Contains(k.Id))
                .Select(k => new { Id = k.Id.Value, k.Name })
                .ToDictionaryAsync(k => k.Id, k => k.Name, ct);
        }

        var items = pageRows.Select(e => new AuditLogEntry(
            e.Id.Value,
            e.Timestamp,
            e.Actor,
            e.ActorId,
            e.Action,
            e.KeyIdGuid,
            e.KeyIdGuid.HasValue && keyNames.TryGetValue(e.KeyIdGuid.Value, out var kn) ? kn : null,
            e.KeyVersion,
            e.Mechanism,
            e.Success,
            e.Error)).ToList();

        return new AuditLogPage(items, query.Page, query.PageSize, totalCount);
    }
}
