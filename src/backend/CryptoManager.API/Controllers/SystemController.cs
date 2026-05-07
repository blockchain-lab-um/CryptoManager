using CryptoManager.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class SystemController : ControllerBase
{
    private readonly ISoftCaBootstrapper _softCaBootstrapper;

    public SystemController(ISoftCaBootstrapper softCaBootstrapper)
    {
        _softCaBootstrapper = softCaBootstrapper;
    }

    [HttpGet("ca-certificate")]
    public async Task<IActionResult> DownloadSystemCaCertificate(CancellationToken ct)
    {
        var ca = await _softCaBootstrapper.EnsureInitializedAsync(ct);
        return File(
            fileContents: ca.CertificateDer,
            contentType: "application/pkix-cert",
            fileDownloadName: "cryptomanager-softca.cer");
    }
}
