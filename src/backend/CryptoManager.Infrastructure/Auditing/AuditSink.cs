using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using CryptoManager.Infrastructure.Persistence.EntityFramework;

namespace CryptoManager.Infrastructure.Auditing;

public sealed class AuditSink : IAuditSink
{
    private readonly AppDbContext _db;

    public AuditSink(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(AuditEvent evt)
    {
        _db.AuditEvents.Add(evt);
        await _db.SaveChangesAsync();
    }
}
