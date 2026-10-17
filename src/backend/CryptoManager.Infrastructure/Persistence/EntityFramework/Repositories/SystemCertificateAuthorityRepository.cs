using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SystemCertificateAuthorityRepository : ISystemCertificateAuthorityRepository
{
    private readonly AppDbContext _db;

    public SystemCertificateAuthorityRepository(AppDbContext db) => _db = db;

    public Task<SystemCertificateAuthority?> GetActiveByNameAsync(string name, CancellationToken ct = default)
        => _db.SystemCertificateAuthorities
            .FirstOrDefaultAsync(x => x.Name == name && x.IsActive, ct);

    public Task AddAsync(SystemCertificateAuthority authority, CancellationToken ct = default)
        => _db.SystemCertificateAuthorities.AddAsync(authority, ct).AsTask();
}
