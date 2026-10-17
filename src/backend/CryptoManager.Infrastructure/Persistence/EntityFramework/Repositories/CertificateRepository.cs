using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class CertificateRepository : ICertificateRepository
{
    private readonly AppDbContext _db;

    public CertificateRepository(AppDbContext db) => _db = db;

    public async Task<Certificate?> GetByIdAsync(CertificateId id, CancellationToken ct = default)
        => await _db.Certificates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Certificate?> GetByThumbprintAsync(string thumbprint, CancellationToken ct = default)
        => await _db.Certificates.FirstOrDefaultAsync(c => c.Thumbprint == thumbprint, ct);

    public async Task<Certificate?> GetByEnrollmentIdAsync(string enrollmentId, CancellationToken ct = default)
        => await _db.Certificates.FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId, ct);

    public async Task<Certificate?> GetActiveCertificateAsync(KeyVersionId keyVersionId, CancellationToken ct = default)
        => await _db.Certificates.FirstOrDefaultAsync(
            c => c.KeyVersionId == keyVersionId && c.Status == Domain.Enums.CertificateStatus.Active, ct);

    public async Task<IReadOnlyList<Certificate>> GetAllForKeyVersionAsync(KeyVersionId keyVersionId, CancellationToken ct = default)
        => await _db.Certificates
            .Where(c => c.KeyVersionId == keyVersionId)
            .ToListAsync(ct);

    public async Task AddAsync(Certificate certificate, CancellationToken ct = default)
        => await _db.Certificates.AddAsync(certificate, ct);

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
    {
        _db.Certificates.Update(certificate);
        return Task.CompletedTask;
    }
}