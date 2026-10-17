using CryptoManager.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CryptoManager.Infrastructure.Identity;

public sealed class UserLookup : IUserLookup
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserLookup(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<IReadOnlyDictionary<string, string>> GetUsernamesByIdsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<string, string>();

        var users = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return users
            .Where(u => u.UserName is not null)
            .ToDictionary(u => u.Id, u => u.UserName!);
    }
}