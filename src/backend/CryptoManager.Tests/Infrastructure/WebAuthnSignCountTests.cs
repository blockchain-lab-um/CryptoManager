using System.Text.Json;
using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Identity;
using CryptoManager.Infrastructure.Identity.WebAuthn;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace CryptoManager.Tests.Infrastructure;

public sealed class WebAuthnSignCountTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    private sealed class StubFido2(uint signCount) : IFido2
    {
        public CredentialCreateOptions RequestNewCredential(RequestNewCredentialParams p) => throw new NotImplementedException();
        public Task<RegisteredPublicKeyCredential> MakeNewCredentialAsync(MakeNewCredentialParams p, CancellationToken ct = default) => throw new NotImplementedException();
        public AssertionOptions GetAssertionOptions(GetAssertionOptionsParams p) => throw new NotImplementedException();
        public Task<VerifyAssertionResult> MakeAssertionAsync(MakeAssertionParams p, CancellationToken ct = default)
            => Task.FromResult(new VerifyAssertionResult { SignCount = signCount });
    }

    private sealed class StubRepo(UserCredential stored, Action<UserCredential> onUpdate) : IUserCredentialRepository
    {
        public Task AddAsync(UserCredential c, CancellationToken ct) => Task.CompletedTask;
        public Task<UserCredential?> GetByCredentialIdAsync(byte[] id, CancellationToken ct) => Task.FromResult<UserCredential?>(stored);
        public Task<IReadOnlyList<UserCredential>> ListByUserAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<UserCredential>>([]);
        public Task<UserCredential?> GetByIdForUserAsync(Guid id, string userId, CancellationToken ct) => Task.FromResult<UserCredential?>(null);
        public Task UpdateAsync(UserCredential c, CancellationToken ct) { onUpdate(c); return Task.CompletedTask; }
        public Task DeleteAsync(UserCredential c, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> CredentialIdInUseAsync(byte[] id, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class StubCache(string payload) : IWebAuthnChallengeCache
    {
        public Task PutAsync(string s, string p, TimeSpan t, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> ConsumeAsync(string s, CancellationToken ct) => Task.FromResult<string?>(payload);
    }

    [Fact]
    public async Task CompleteAssertion_persists_updated_sign_count()
    {
        const uint initial = 5u, updated = 10u;
        var credId = new byte[] { 1, 2, 3 };
        var credIdB64 = WebEncoders.Base64UrlEncode(credId);

        var stored = new UserCredential
        {
            UserId = "u1", CredentialId = credId,
            PublicKey = Array.Empty<byte>(), UserHandle = Array.Empty<byte>(),
            SignCount = initial, Transports = Array.Empty<string>(),
            Nickname = "Test", CreatedAt = DateTimeOffset.UtcNow
        };

        UserCredential? captured = null;

        // Manually build ChallengeEnvelope JSON (Kind=1 is Assert; file-scoped type not accessible from tests).
        var assertionOptionsJson = """{"challenge":"AAAAAAAAAAAAAAAAAAAAAA","timeout":60000,"rpId":"localhost","allowCredentials":[],"userVerification":"preferred","status":"ok","errorMessage":""}""";
        var envelopeJson = $"{{\"kind\":1,\"userId\":null,\"optionsJson\":{JsonSerializer.Serialize(assertionOptionsJson)}}}";

        var assertionResponse = JsonSerializer.Deserialize<JsonElement>(
            $"{{\"id\":\"{credIdB64}\",\"rawId\":\"{credIdB64}\",\"response\":{{\"clientDataJSON\":\"AAAA\",\"authenticatorData\":\"AAAA\",\"signature\":\"AAAA\"}},\"type\":\"public-key\"}}");

        var sut = new WebAuthnService(
            new StubFido2(updated),
            new StubRepo(stored, c => captured = c),
            new StubCache(envelopeJson),
            null!, null!, new FixedClock());

        await sut.CompleteAssertionAsync(new CompleteAssertionRequest("s1", assertionResponse), default);

        Assert.NotNull(captured);
        Assert.Equal(updated, captured.SignCount);
    }
}
