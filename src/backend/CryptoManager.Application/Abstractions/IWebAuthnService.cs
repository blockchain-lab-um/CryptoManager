namespace CryptoManager.Application.Abstractions;

public interface IWebAuthnService
{
    Task<BeginRegistrationResponse> BeginRegistrationAsync(string userId, string userName, string? attachment, CancellationToken ct);
    Task<CompleteRegistrationResponse> CompleteRegistrationAsync(string userId, CompleteRegistrationRequest req, CancellationToken ct);
    Task<BeginAssertionResponse> BeginAssertionAsync(string? userName, CancellationToken ct);
    Task<AssertionResult> CompleteAssertionAsync(CompleteAssertionRequest req, CancellationToken ct); // throws on failure
}
