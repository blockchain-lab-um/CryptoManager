using CryptoManager.API.DTOs.Auth;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.Exceptions;
using CryptoManager.Infrastructure.Identity;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers;

[ApiController]
[Route("api/auth/webauthn")]
public sealed class WebAuthnController(
    IWebAuthnService svc,
    IUserCredentialRepository repo,
    UserManager<ApplicationUser> users,
    TokenService tokens,
    ICurrentUser current) : ControllerBase
{
    [Authorize]
    [HttpPost("register/begin")]
    public async Task<ActionResult<BeginRegistrationResponse>> RegisterBegin([FromBody] BeginRegistrationRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(current.UserId);
        if (user is null) return Unauthorized();
        return await svc.BeginRegistrationAsync(user.Id, user.UserName!, req.AuthenticatorAttachment, ct);
    }

    [Authorize]
    [HttpPost("register/complete")]
    public async Task<ActionResult<CompleteRegistrationResponse>> RegisterComplete([FromBody] CompleteRegistrationRequest req, CancellationToken ct)
    {
        // WebAuthnException (Challenge expired / payload invalid / wrong user) must return 400, not 500.
        // ErrorHandlingMiddleware only maps DomainException→400 and NotFoundException→404; everything else is 500.
        try { return await svc.CompleteRegistrationAsync(current.UserId, req, ct); }
        catch (WebAuthnException e) { return BadRequest(new { error = e.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("login/begin")]
    public async Task<ActionResult<BeginAssertionResponse>> LoginBegin([FromBody] BeginAssertionRequest req, CancellationToken ct)
    {
        // "Authentication failed." is an expected auth failure — return 401 to match password-login semantics.
        // Single message for both unknown-user and no-passkeys paths (username enumeration prevention).
        try { return await svc.BeginAssertionAsync(req.UserName, ct); }
        catch (WebAuthnException e) { return Unauthorized(new { error = e.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("login/complete")]
    public async Task<ActionResult<LoginResponseDto>> LoginComplete([FromBody] CompleteAssertionRequest req, CancellationToken ct)
    {
        // "Challenge expired" and "Unknown credential" are expected 400s (WebAuthnException).
        // Fido2.AspNet 4.x throws Fido2VerificationException on counter regression / bad signature — return 401.
        // TYPE NAME MUST BE VERIFIED: see Step 0 above. If the name differs from Fido2VerificationException,
        // this catch clause must already have been updated before this line was written.
        // A wrong type makes this catch dead code; counter-regression events return 500.
        AssertionResult assertion;
        try { assertion = await svc.CompleteAssertionAsync(req, ct); }
        catch (WebAuthnException e) { return BadRequest(new { error = e.Message }); }
        catch (Fido2VerificationException e) { return Unauthorized(new { error = e.Message }); }

        var user = await users.FindByIdAsync(assertion.UserId) ?? throw new InvalidOperationException("User missing.");
        var roles = await users.GetRolesAsync(user);
        var (token, expiresAt) = tokens.CreateToken(user, roles);
        // LoginResponseDto is a settable class (not a positional record). Use object initializer to match the existing AuthController contract exactly.
        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserName = user.UserName!,
            Roles = roles
        };
    }

    [Authorize]
    [HttpGet("credentials")]
    public async Task<IReadOnlyList<CredentialView>> List(CancellationToken ct)
        => (await repo.ListByUserAsync(current.UserId, ct))
            .Select(c => new CredentialView(c.Id, c.Nickname, c.CreatedAt, c.LastUsedAt, c.Transports, c.IsBackedUp))
            .ToList();

    [Authorize]
    [HttpPatch("credentials/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameRequest req, CancellationToken ct)
    {
        var c = await repo.GetByIdForUserAsync(id, current.UserId, ct);
        if (c is null) return NotFound();
        c.Nickname = req.Nickname.Trim();
        await repo.UpdateAsync(c, ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("credentials/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var c = await repo.GetByIdForUserAsync(id, current.UserId, ct);
        if (c is null) return NotFound();
        await repo.DeleteAsync(c, ct);
        return NoContent();
    }

    public sealed record CredentialView(Guid Id, string Nickname, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, string[] Transports, bool IsBackedUp);
    public sealed record RenameRequest(string Nickname);
}
