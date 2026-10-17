using CryptoManager.Application.Abstractions;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db) => _db = db;

    public async Task SaveAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}