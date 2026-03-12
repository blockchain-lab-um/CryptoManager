using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;

namespace CryptoManager.Application.UseCases;

public class ListKeysUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICurrentUser _currentUser;

    public ListKeysUseCase(IKeyRepository keyRepository, ICurrentUser currentUser)
    {
        _keyRepository = keyRepository;
        _currentUser = currentUser;
    }

    public async Task<ListKeysResult> ExecuteAsync()
    {
        var keys = _currentUser.IsInRole("Admin")
            ? await _keyRepository.ListAllAsync()
            : await _keyRepository.ListByOwnerAsync(_currentUser.UserId);

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