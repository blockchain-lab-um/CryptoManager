using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface IAuditRepository
{
    Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}