using CryptoManager.API.DTOs.Crypto;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "CanOperate")]
public sealed class CryptoController : ControllerBase
{
    private readonly SignDigestUseCase _signDigest;
    private readonly SignFileUseCase _signFile;

    public CryptoController(SignDigestUseCase signDigest, SignFileUseCase signFile)
    {
        _signDigest = signDigest;
        _signFile = signFile;
    }

    // POST /api/crypto/sign
    [HttpPost("sign")]
    [ProducesResponseType(typeof(SignDigestResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SignDigestResponseDto>> SignDigest(
        [FromBody] SignDigestRequestDto request,
        CancellationToken ct)
    {
        var kid = new KeyId(Guid.Parse(request.KeyId));
        var mechanism = Mechanism.Parse(request.Mechanism);

        byte[] digest;
        try
        {
            digest = Convert.FromBase64String(request.DigestBase64);
        }
        catch (FormatException)
        {
            throw new DomainException("DigestBase64 is not valid base64.");
        }

        var result = await _signDigest.ExecuteAsync(new SignDigestCommand(
            KeyId: kid,
            Mechanism: mechanism,
            Digest: digest
        ));

        return Ok(new SignDigestResponseDto
        {
            KeyId = result.KeyId.Value.ToString(),
            KeyVersion = result.KeyVersion,
            Mechanism = result.Mechanism.Name,
            Encoding = result.SignatureEncoding.ToString(),
            SignatureBase64 = Convert.ToBase64String(result.Signature),
            AuditEventId = result.AuditEventId.Value.ToString()
        });
    }

    // POST /api/crypto/sign-file
    [HttpPost("sign-file")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> SignFile(
        [FromForm] SignFileRequestDto dto,
        CancellationToken ct)
    {
        var keyId = new KeyId(Guid.Parse(dto.KeyId));
        var mechanism = Mechanism.Parse(dto.Mechanism);

        byte[] fileBytes;
        await using (var ms = new MemoryStream())
        {
            await dto.File.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
        }

        var result = await _signFile.ExecuteAsync(
            new SignFileCommand(
                KeyId: keyId,
                Mechanism: mechanism,
                OriginalFileName: dto.File.FileName,
                OriginalContentType: dto.File.ContentType,
                FileBytes: fileBytes),
            ct);

        Response.Headers["X-Audit-Event-Id"] = result.AuditEventId.ToString();
        Response.Headers["X-Key-Id"] = result.KeyId.ToString();
        Response.Headers["X-Key-Version"] = result.KeyVersion.ToString();
        Response.Headers["X-Mechanism"] = result.Mechanism.Name;
        Response.Headers["X-Signed-Format"] = result.SignedFormat;
        Response.Headers["X-File-Name"] = result.OutputFileName;

        return File(
            fileContents: result.SignedFileBytes,
            contentType: result.OutputContentType,
            fileDownloadName: result.OutputFileName);
    }
}