using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;

namespace CryptoManager.Application.UseCases;

public class ListKeysUseCase
{
    private readonly IKeyRepository _keyRepository;

    public ListKeysUseCase(IKeyRepository keyRepository)
    {
        _keyRepository = keyRepository;
    }

    public async Task<ListKeysResult> ExecuteAsync()
    {
        var keys = await _keyRepository.ListAllAsync();

        var summaries = keys.Select(k => new KeySummary(
            k.Id,
            k.Name,
            k.Purpose,
            k.State,
            k.Versions.Count,
            k.Versions.FirstOrDefault(v => v.Status == KeyVersionStatus.Primary)?.Version,
            k.CreatedAt
        )).ToList();

        return new ListKeysResult(summaries);
    }
}
