using System.Collections.Concurrent;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.Exceptions;

namespace CryptoManager.Infrastructure.Identity.WebAuthn;

public sealed class InMemoryWebAuthnChallengeCache : IWebAuthnChallengeCache
{
    // Hard cap on stored entries. `login/begin` is anonymous and allocates one entry per call,
    // so an unbounded dictionary is a cheap memory-amplification vector for unauthenticated traffic.
    // 10k is comfortably above realistic concurrent in-flight handshakes and small enough to bound RAM.
    private const int MaxEntries = 10_000;

    private readonly ConcurrentDictionary<string, (string Payload, DateTimeOffset ExpiresAt)> _store = new();
    private readonly IClock _clock;

    public InMemoryWebAuthnChallengeCache(IClock clock) => _clock = clock;

    public Task PutAsync(string sessionId, string payloadJson, TimeSpan ttl, CancellationToken ct)
    {
        // If at capacity, opportunistically sweep expired entries first; if still full, refuse.
        // Throwing WebAuthnException maps to 400 via the controller catch (Task 8), not 500.
        if (_store.Count >= MaxEntries)
        {
            var now = _clock.UtcNow;
            foreach (var kvp in _store)
                if (kvp.Value.ExpiresAt <= now)
                    _store.TryRemove(kvp.Key, out _);

            if (_store.Count >= MaxEntries)
                throw new WebAuthnException("Server is busy handling WebAuthn requests. Please try again.");
        }

        _store[Key(sessionId)] = (payloadJson, _clock.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task<string?> ConsumeAsync(string sessionId, CancellationToken ct)
    {
        // TryRemove is atomic — exactly one concurrent caller can win.
        if (!_store.TryRemove(Key(sessionId), out var entry)) return Task.FromResult<string?>(null);
        if (entry.ExpiresAt <= _clock.UtcNow) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(entry.Payload);
    }

    private static string Key(string sessionId) => $"webauthn:{sessionId}";
}
