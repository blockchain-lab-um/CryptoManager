using CryptoManager.Application.Abstractions;
using CryptoManager.Application.Exceptions;
using CryptoManager.Infrastructure.Identity.WebAuthn;

namespace CryptoManager.Tests.Infrastructure;

public sealed class WebAuthnChallengeCacheTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }

    [Fact]
    public async Task Consume_returns_value_first_call_then_null()
    {
        var sut = new InMemoryWebAuthnChallengeCache(new FixedClock());
        await sut.PutAsync("s1", "{\"x\":1}", TimeSpan.FromMinutes(5), default);

        Assert.Equal("{\"x\":1}", await sut.ConsumeAsync("s1", default));
        Assert.Null(await sut.ConsumeAsync("s1", default));
    }

    [Fact]
    public async Task Consume_returns_null_for_unknown_session()
    {
        var sut = new InMemoryWebAuthnChallengeCache(new FixedClock());
        Assert.Null(await sut.ConsumeAsync("never-stored", default));
    }

    [Fact]
    public async Task Consume_after_ttl_expiry_returns_null()
    {
        var clock = new FixedClock { UtcNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        var sut = new InMemoryWebAuthnChallengeCache(clock);
        await sut.PutAsync("s1", "{\"x\":1}", TimeSpan.FromSeconds(5), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(6);
        Assert.Null(await sut.ConsumeAsync("s1", default));
    }

    [Fact]
    public async Task Put_rejects_once_max_entries_is_reached_and_no_expired_slots_can_be_swept()
    {
        // Memory-amplification guard: anonymous login/begin must not be able to grow the cache unboundedly.
        // When the cache is full of still-live entries, PutAsync must refuse rather than evict a valid challenge.
        var sut = new InMemoryWebAuthnChallengeCache(new FixedClock());
        // MaxEntries is a const inside the implementation; the test fills until it throws.
        var threw = false;
        try
        {
            for (var i = 0; i < 20_000; i++)
                await sut.PutAsync($"s{i}", "payload", TimeSpan.FromMinutes(5), default);
        }
        catch (WebAuthnException) { threw = true; }
        Assert.True(threw, "Cache must refuse new entries once MaxEntries is reached.");
    }

    [Fact]
    public async Task Consume_under_concurrent_callers_yields_value_to_exactly_one()
    {
        // Replay-protection contract: even if N requests race to consume the same session id,
        // only one can observe the payload. ConcurrentDictionary.TryRemove guarantees this.
        var sut = new InMemoryWebAuthnChallengeCache(new FixedClock());
        await sut.PutAsync("s1", "payload", TimeSpan.FromMinutes(5), default);

        const int parallelism = 32;
        using var barrier = new Barrier(parallelism);
        var winners = 0;
        var tasks = Enumerable.Range(0, parallelism).Select(_ => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            var v = await sut.ConsumeAsync("s1", default);
            if (v is not null) Interlocked.Increment(ref winners);
        })).ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(1, winners);
    }
}
