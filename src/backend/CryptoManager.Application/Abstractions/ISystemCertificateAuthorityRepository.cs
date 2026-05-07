using CryptoManager.Domain.Entities;

namespace CryptoManager.Application.Abstractions;

public interface ISystemCertificateAuthorityRepository
{
    Task<SystemCertificateAuthority?> GetActiveByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(SystemCertificateAuthority authority, CancellationToken ct = default);
}
