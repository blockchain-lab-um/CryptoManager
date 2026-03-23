namespace CryptoManager.Application.Abstractions;

public interface IUserLookup
{
    /// <summary>
    /// Returns usernames keyed by user ID for the given set of IDs.
    /// Missing IDs are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetUsernamesByIdsAsync(IEnumerable<string> userIds);
}