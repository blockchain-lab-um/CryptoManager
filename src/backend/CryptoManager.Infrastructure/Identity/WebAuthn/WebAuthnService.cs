// Fido2.AspNet pinned version: 4.0.1
// Verified at: https://github.com/passwordless-lib/fido2-net-lib/blob/v4.0.1/Src/Fido2/IFido2.cs
// Symbols used by this file:
//   - IFido2.RequestNewCredential(RequestNewCredentialParams) → CredentialCreateOptions
//   - IFido2.MakeNewCredentialAsync(MakeNewCredentialParams, CancellationToken) → RegisteredPublicKeyCredential
//   - IFido2.GetAssertionOptions(GetAssertionOptionsParams) → AssertionOptions
//   - IFido2.MakeAssertionAsync(MakeAssertionParams, CancellationToken) → VerifyAssertionResult
//   - RegisteredPublicKeyCredential.{Id, PublicKey, User.Id, SignCount, AttestationFormat, AaGuid, IsBackupEligible (BE), IsBackedUp (BS)}
//   - VerifyAssertionResult.{SignCount}
//   - MakeAssertionAsync failure exception type: Fido2VerificationException
// If any of the above changes, update this comment AND the call sites; do not silently swap names.

using System.Security.Cryptography;
using System.Text.Json;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Identity;
using CryptoManager.Infrastructure.Identity;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CryptoManager.Infrastructure.Identity.WebAuthn;

// Single typed envelope for ALL challenge-cache payloads (registration AND assertion).
// Using one shape prevents a future maintainer from accidentally feeding a registration payload to
// AssertionOptions.FromJson (or vice versa) — every consumer parses the same envelope, switches on Kind,
// then deserializes OptionsJson with the matching FromJson(...). UserId is non-null only for "register".
file enum ChallengeKind { Register, Assert }
file sealed record ChallengeEnvelope(ChallengeKind Kind, string? UserId, string OptionsJson);

public sealed class WebAuthnService(
    IFido2 fido2,
    IUserCredentialRepository repo,
    IWebAuthnChallengeCache cache,
    UserManager<ApplicationUser> users,
    IOptions<WebAuthnOptions> opts,
    IClock clock) : IWebAuthnService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<BeginRegistrationResponse> BeginRegistrationAsync(string userId, string userName, string? attachment, CancellationToken ct)
    {
        var existing = await repo.ListByUserAsync(userId, ct);
        var exclude = existing.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId)).ToList();
        var appUser = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("User missing.");
        if (appUser.WebAuthnUserHandle is null || appUser.WebAuthnUserHandle.Length == 0)
        {
            appUser.WebAuthnUserHandle = RandomNumberGenerator.GetBytes(64);
            var update = await users.UpdateAsync(appUser);
            if (!update.Succeeded)
                throw new InvalidOperationException("Failed to persist WebAuthn user handle.");
        }
        // Name/DisplayName are snapshotted by the authenticator (and synced to Apple/Google/MS clouds) at enrollment.
        // If the user later renames their account, the credential picker on their devices will keep showing the OLD value
        // until they re-enroll the passkey. We accept this WebAuthn-protocol limitation; the passkeys page documents it.
        var fidoUser = new Fido2User { Id = appUser.WebAuthnUserHandle, Name = userName, DisplayName = userName };
        var auth = new AuthenticatorSelection
        {
            UserVerification = UserVerificationRequirement.Preferred,
            ResidentKey = ResidentKeyRequirement.Preferred,
            RequireResidentKey = false, // older YubiKeys lack discoverable creds; allow allow-list fallback
            AuthenticatorAttachment = attachment switch
            {
                "platform" => AuthenticatorAttachment.Platform,
                "cross-platform" => AuthenticatorAttachment.CrossPlatform,
                _ => null
            }
        };
        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = exclude,
            AuthenticatorSelection = auth,
            AttestationPreference = AttestationConveyancePreference.None
        });
        var optionsJson = options.ToJson();
        var sessionId = Guid.NewGuid().ToString("N");
        var envelopeJson = JsonSerializer.Serialize(new ChallengeEnvelope(ChallengeKind.Register, userId, optionsJson), Json);
        await cache.PutAsync(sessionId, envelopeJson, TimeSpan.FromSeconds(opts.Value.ChallengeTtlSeconds), ct);
        return new BeginRegistrationResponse(sessionId, optionsJson);
    }

    public async Task<CompleteRegistrationResponse> CompleteRegistrationAsync(string userId, CompleteRegistrationRequest req, CancellationToken ct)
    {
        var cached = await cache.ConsumeAsync(req.SessionId, ct) ?? throw new WebAuthnException("Challenge expired.");
        var envelope = JsonSerializer.Deserialize<ChallengeEnvelope>(cached, Json)
            ?? throw new WebAuthnException("Challenge payload invalid.");
        if (envelope.Kind != ChallengeKind.Register)
            throw new WebAuthnException("Challenge payload invalid."); // session was an assertion challenge; fail closed
        if (!string.Equals(envelope.UserId, userId, StringComparison.Ordinal))
            throw new WebAuthnException("Challenge does not belong to the current user.");
        var options = CredentialCreateOptions.FromJson(envelope.OptionsJson);
        var attestation = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(req.AttestationResponse.GetRawText(), Json)!;

        IsCredentialIdUniqueToUserAsyncDelegate uniq = async (args, _) => !await repo.CredentialIdInUseAsync(args.CredentialId, ct);
        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestation,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = uniq
        }, ct);

        var entity = new UserCredential
        {
            UserId = userId,
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            UserHandle = result.User.Id,
            SignCount = result.SignCount,
            AttestationFormat = result.AttestationFormat,
            AaGuid = result.AaGuid,
            // Persist transports as the lowercase WebAuthn strings the browser sent (the frontend already passes them
            // through verbatim from getTransports()). Do NOT use enum.ToString() — it produces PascalCase ("Usb"/"Nfc")
            // which would diverge from the spec values the frontend filters on.
            Transports = (req.AttestationResponse.TryGetProperty("response", out var resp) &&
                          resp.TryGetProperty("transports", out var t) &&
                          t.ValueKind == JsonValueKind.Array)
                ? t.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).ToArray()
                : Array.Empty<string>(),
            IsBackupEligible = result.IsBackupEligible,
            IsBackedUp = result.IsBackedUp,
            Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? "Passkey" : req.Nickname!.Trim(),
            CreatedAt = clock.UtcNow
        };
        await repo.AddAsync(entity, ct);
        return new CompleteRegistrationResponse(entity.Id, entity.Nickname);
    }

    public async Task<BeginAssertionResponse> BeginAssertionAsync(string? userName, CancellationToken ct)
    {
        var allow = new List<PublicKeyCredentialDescriptor>();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            // Use a single generic error for both "unknown user" and "no passkeys registered" to prevent
            // account-existence and passkey-enrollment enumeration. Distinct messages would let an attacker
            // probe which usernames exist and which have passkeys enrolled — a regression from the current auth flow.
            var u = await users.FindByNameAsync(userName);
            var creds = u is null ? [] : await repo.ListByUserAsync(u.Id, ct);
            if (u is null || creds.Count == 0)
                throw new WebAuthnException("Authentication failed.");
            foreach (var c in creds)
                allow.Add(new PublicKeyCredentialDescriptor(c.CredentialId));
        }
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allow,
            UserVerification = UserVerificationRequirement.Preferred
        });
        var optionsJson = options.ToJson();
        var sessionId = Guid.NewGuid().ToString("N");
        var envelopeJson = JsonSerializer.Serialize(new ChallengeEnvelope(ChallengeKind.Assert, UserId: null, optionsJson), Json);
        await cache.PutAsync(sessionId, envelopeJson, TimeSpan.FromSeconds(opts.Value.ChallengeTtlSeconds), ct);
        return new BeginAssertionResponse(sessionId, optionsJson);
    }

    public async Task<AssertionResult> CompleteAssertionAsync(CompleteAssertionRequest req, CancellationToken ct)
    {
        var cached = await cache.ConsumeAsync(req.SessionId, ct) ?? throw new WebAuthnException("Challenge expired.");
        var envelope = JsonSerializer.Deserialize<ChallengeEnvelope>(cached, Json)
            ?? throw new WebAuthnException("Challenge payload invalid.");
        if (envelope.Kind != ChallengeKind.Assert)
            throw new WebAuthnException("Challenge payload invalid."); // session was a registration challenge; fail closed
        var options = AssertionOptions.FromJson(envelope.OptionsJson);
        var assertion = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(req.AssertionResponse.GetRawText(), Json)!;
        var credIdBytes = WebEncoders.Base64UrlDecode(assertion.Id);
        var stored = await repo.GetByCredentialIdAsync(credIdBytes, ct) ?? throw new WebAuthnException("Unknown credential.");

        // userHandle is OPTIONAL in the assertion response: present for discoverable-credential (resident-key) logins,
        // typically absent for allow-list logins (e.g. older YubiKeys without resident creds). Only enforce when present.
        IsUserHandleOwnerOfCredentialIdAsync owner = (args, _) =>
        {
            if (args.UserHandle is null || args.UserHandle.Length == 0)
                return Task.FromResult(stored.CredentialId.SequenceEqual(args.CredentialId));
            return Task.FromResult(
                stored.UserHandle.SequenceEqual(args.UserHandle) &&
                stored.CredentialId.SequenceEqual(args.CredentialId));
        };

        var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = assertion,
            OriginalOptions = options,
            StoredPublicKey = stored.PublicKey,
            StoredSignatureCounter = stored.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = owner
        }, ct);

        // Library throws on counter regression; do NOT duplicate the check here (would never run on regression, and would
        // re-throw on the legitimate synced-passkey "0 forever" case which the library already permits).

        stored.SignCount = result.SignCount;
        stored.LastUsedAt = clock.UtcNow;
        await repo.UpdateAsync(stored, ct);
        return new AssertionResult(stored.UserId, stored.Id);
    }
}
