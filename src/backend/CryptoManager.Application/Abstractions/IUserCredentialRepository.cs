using CryptoManager.Domain.Identity;

namespace CryptoManager.Application.Abstractions;

public interface IUserCredentialRepository
{
    Task AddAsync(UserCredential credential, CancellationToken ct);
    Task<UserCredential?> GetByCredentialIdAsync(byte[] credentialId, CancellationToken ct);
    Task<IReadOnlyList<UserCredential>> ListByUserAsync(string userId, CancellationToken ct);
    Task<UserCredential?> GetByIdForUserAsync(Guid id, string userId, CancellationToken ct);
    Task UpdateAsync(UserCredential credential, CancellationToken ct);
    Task DeleteAsync(UserCredential credential, CancellationToken ct);
    Task<bool> CredentialIdInUseAsync(byte[] credentialId, CancellationToken ct);
}
