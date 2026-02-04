using CryptoManager.API.DTOs.Crypto;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class CryptoController : ControllerBase
    {
        private readonly SignDigestUseCase _signDigest;

        public CryptoController(SignDigestUseCase signDigest)
        {
            _signDigest = signDigest;
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

            var cmd = new SignDigestCommand(
                KeyId: kid,
                Mechanism: mechanism,
                Digest: digest,
                KeyVersion: request.Version
            );

            var actor = GetActor();
            var requestId = HttpContext.TraceIdentifier;

            var result = await _signDigest.ExecuteAsync(cmd, actor, requestId);

            var test = Convert.FromBase64String(Convert.ToBase64String(result.Signature));

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

        private string GetActor()
        {
            if (User?.Identity?.IsAuthenticated == true)
                return User.Identity!.Name ?? "authenticated-user";

            return "dev";
        }
    }
}
