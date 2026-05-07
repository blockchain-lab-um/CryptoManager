using CryptoManager.Application.DTOs;

namespace CryptoManager.Application.Abstractions;

public interface ISoftCaBootstrapper
{
    Task<SoftCaMaterial> EnsureInitializedAsync(CancellationToken ct = default);
}
