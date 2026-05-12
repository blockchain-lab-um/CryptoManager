namespace CryptoManager.Application.Abstractions;

public interface IWebAuthnChallengeCache
{
    Task PutAsync(string sessionId, string payloadJson, TimeSpan ttl, CancellationToken ct);
    Task<string?> ConsumeAsync(string sessionId, CancellationToken ct); // returns then deletes
}
