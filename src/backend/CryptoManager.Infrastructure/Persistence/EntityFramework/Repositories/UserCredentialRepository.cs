using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Identity;
using CryptoManager.Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class UserCredentialRepository(AppDbContext db) : IUserCredentialRepository
{
    public Task AddAsync(UserCredential c, CancellationToken ct)
    {
        db.UserCredentials.Add(c); return db.SaveChangesAsync(ct);
    }

    public Task<UserCredential?> GetByCredentialIdAsync(byte[] id, CancellationToken ct) =>
        db.UserCredentials.FirstOrDefaultAsync(x => x.CredentialId == id, ct);

    public async Task<IReadOnlyList<UserCredential>> ListByUserAsync(string userId, CancellationToken ct) =>
        await db.UserCredentials.Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public Task<UserCredential?> GetByIdForUserAsync(Guid id, string userId, CancellationToken ct) =>
        db.UserCredentials.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public Task UpdateAsync(UserCredential c, CancellationToken ct)
    {
        db.UserCredentials.Update(c); return db.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(UserCredential c, CancellationToken ct)
    {
        db.UserCredentials.Remove(c); return db.SaveChangesAsync(ct);
    }

    public Task<bool> CredentialIdInUseAsync(byte[] id, CancellationToken ct) =>
        db.UserCredentials.AnyAsync(x => x.CredentialId == id, ct);
}
