using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;

namespace CryptoManager.Application.UseCases;

public class ListKeysUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUserLookup _userLookup;

    public ListKeysUseCase(IKeyRepository keyRepository, ICurrentUser currentUser, IUserLookup userLookup)
    {
        _keyRepository = keyRepository;
        _currentUser = currentUser;
        _userLookup = userLookup;
    }

    public async Task<ListKeysResult> ExecuteAsync()
    {
        var isAdmin = _currentUser.IsInRole("Admin");

        var keys = isAdmin
            ? await _keyRepository.ListAllAsync()
            : await _keyRepository.ListByOwnerAsync(_currentUser.UserId);

        IReadOnlyDictionary<string, string> ownerNames = new Dictionary<string, string>();
        if (isAdmin)
        {
            var ownerIds = keys.Select(k => k.OwnerId).Distinct();
            ownerNames = await _userLookup.GetUsernamesByIdsAsync(ownerIds);
        }

        var summaries = keys.Select(k => new KeySummary(
            k.Id,
            k.Name,
            k.Purpose,
            k.State,
            k.Versions.Count,
            k.Versions.FirstOrDefault(v => v.Status == KeyVersionStatus.Primary)?.Version,
            k.CreatedAt,
            isAdmin ? ownerNames.GetValueOrDefault(k.OwnerId) : null
        )).ToList();

        return new ListKeysResult(summaries);
    }
}