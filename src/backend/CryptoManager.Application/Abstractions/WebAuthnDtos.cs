using System.Text.Json;

namespace CryptoManager.Application.Abstractions;

public sealed record BeginRegistrationRequest(string? AuthenticatorAttachment); // "platform" | "cross-platform" | null
// Options is the JSON string produced by Fido2NetLib's CredentialCreateOptions.ToJson() / AssertionOptions.ToJson().
// We pass it through verbatim so the browser receives base64url-encoded byte fields. DO NOT type this as the strongly-typed
// Fido2 options object — System.Text.Json would re-serialize byte[] fields incorrectly (number arrays / standard base64).
public sealed record BeginRegistrationResponse(string SessionId, string OptionsJson);
public sealed record CompleteRegistrationRequest(string SessionId, JsonElement AttestationResponse, string? Nickname);
public sealed record CompleteRegistrationResponse(Guid CredentialId, string Nickname);

public sealed record BeginAssertionRequest(string? UserName);
public sealed record BeginAssertionResponse(string SessionId, string OptionsJson);
public sealed record CompleteAssertionRequest(string SessionId, JsonElement AssertionResponse);
// Slim result returned to the controller. We intentionally do NOT return the UserCredential entity
// (the controller only needs the owner's UserId; leaking the entity widens the Application boundary
// and exposes mutable persistence fields like SignCount/LastUsedAt to the API layer).
public sealed record AssertionResult(string UserId, Guid CredentialId);
