using CryptoManager.API.DTOs.Crypto;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace CryptoManager.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "CanOperate")]
public sealed class CryptoController : ControllerBase
{
    private readonly SignDigestUseCase _signDigest;
    private readonly SignDocumentUseCase _signDocument;

    public CryptoController(SignDigestUseCase signDigest, SignDocumentUseCase signDocument)
    {
        _signDigest   = signDigest;
        _signDocument = signDocument;
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

        var stamp = new StampOptions(
            Enabled: dto.AddStamp,
            X: ParseInvariantNullableFloat(dto.StampX, nameof(dto.StampX)),
            Y: ParseInvariantNullableFloat(dto.StampY, nameof(dto.StampY)),
            Width: ParseInvariantNullableFloat(dto.StampWidth, nameof(dto.StampWidth)),
            Height: ParseInvariantNullableFloat(dto.StampHeight, nameof(dto.StampHeight)),
            RotationDegrees: ParseInvariantNullableFloat(dto.StampRotation, nameof(dto.StampRotation)) ?? 0f);

        var result = await _signDocument.ExecuteAsync(
            new SignFileCommand(
                KeyId: keyId,
                Mechanism: mechanism,
                OriginalFileName: dto.File.FileName,
                OriginalContentType: dto.File.ContentType,
                FileBytes: fileBytes,
                Stamp: stamp),
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

    private static float? ParseInvariantNullableFloat(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw new DomainException($"{fieldName} must be a valid decimal number using '.' as the decimal separator.");
    }
}
