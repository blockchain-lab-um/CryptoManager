using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.Abstractions;

public interface ICertificateRepository
{
    Task<Certificate?> GetByIdAsync(CertificateId id, CancellationToken ct = default);
    Task<Certificate?> GetByThumbprintAsync(string thumbprint, CancellationToken ct = default);
    Task<Certificate?> GetByEnrollmentIdAsync(string enrollmentId, CancellationToken ct = default);

    /// <summary>Returns the single Active certificate for this KeyVersion, or null if none exists.</summary>
    Task<Certificate?> GetActiveCertificateAsync(KeyVersionId keyVersionId, CancellationToken ct = default);

    Task<IReadOnlyList<Certificate>> GetAllForKeyVersionAsync(KeyVersionId keyVersionId, CancellationToken ct = default);

    Task AddAsync(Certificate certificate, CancellationToken ct = default);

    /// <summary>
    /// Marks the entity as modified in the change tracker. Does not call SaveChanges;
    /// use IUnitOfWork.SaveAsync to commit.
    /// </summary>
    Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
}