namespace CryptoManager.Application.Abstractions;

/// <summary>
/// Commits all pending changes tracked by the underlying EF Core DbContext.
/// Repositories do NOT call SaveChanges themselves — use cases call SaveAsync
/// exactly once at the end to ensure atomicity across multiple repository operations.
/// </summary>
public interface IUnitOfWork
{
    Task SaveAsync(CancellationToken ct = default);
}