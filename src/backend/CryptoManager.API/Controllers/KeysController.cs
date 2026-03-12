using CryptoManager.API.DTOs.Keys;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class KeysController : ControllerBase
    {
        private readonly CreateKeyUseCase _createKey;
        private readonly RotateKeyUseCase _rotateKey;
        private readonly GetPublicKeyUseCase _getPublicKey;
        private readonly ListKeysUseCase _listKeys;
        private readonly DeleteKeyUseCase _deleteKey;

        public KeysController(
            CreateKeyUseCase createKey,
            RotateKeyUseCase rotateKey,
            GetPublicKeyUseCase getPublicKey,
            ListKeysUseCase listKeys,
            DeleteKeyUseCase deleteKey)
        {
            _createKey = createKey;
            _rotateKey = rotateKey;
            _getPublicKey = getPublicKey;
            _listKeys = listKeys;
            _deleteKey = deleteKey;
        }

        // GET /api/keys
        [HttpGet]
        [ProducesResponseType(typeof(ListKeysResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<ListKeysResponseDto>> List(CancellationToken ct)
        {
            var result = await _listKeys.ExecuteAsync();

            return Ok(new ListKeysResponseDto
            {
                Keys = result.Keys.Select(k => new KeySummaryDto
                {
                    KeyId = k.KeyId.Value.ToString(),
                    Name = k.Name,
                    Purpose = k.Purpose.ToString(),
                    State = k.State.ToString(),
                    VersionCount = k.VersionCount,
                    PrimaryVersion = k.PrimaryVersion,
                    CreatedAt = k.CreatedAt
                }).ToList()
            });
        }

        // POST /api/keys
        [HttpPost]
        [ProducesResponseType(typeof(CreateKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<CreateKeyResponseDto>> Create(
            [FromBody] CreateKeyRequestDto request,
            CancellationToken ct)
        {
            // Map API -> Domain/Application
            var purpose = ParsePurpose(request.Purpose);

            var allowedMechanisms = request.AllowedMechanisms
                .Select(Mechanism.Parse)
                .ToArray();

            var cmd = new CreateKeyCommand(
                Name: request.Name.Trim(),
                KeyPurpose: purpose,
                AllowedMechanisms: allowedMechanisms
            );

            var actor = GetActor();
            var requestId = HttpContext.TraceIdentifier;

            var result = await _createKey.ExecuteAsync(cmd, actor, requestId);

            return Ok(new CreateKeyResponseDto
            {
                KeyId = result.KeyId.Value.ToString(),
                Name = result.Name,
                Purpose = result.KeyPurpose.ToString(),
                PrimaryVersion = result.PrimaryVersion,
                PublicKeyPem = result.PublicKey.Pem
            });
        }

        // POST /api/keys/{keyId}/rotate
        [HttpPost("{keyId}/rotate")]
        [ProducesResponseType(typeof(RotateKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RotateKeyResponseDto>> Rotate(
            [FromRoute] string keyId,
            CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));

            var cmd = new RotateKeyCommand(kid);

            var actor = GetActor();
            var requestId = HttpContext.TraceIdentifier;

            var result = await _rotateKey.ExecuteAsync(cmd, actor, requestId);

            return Ok(new RotateKeyResponseDto
            {
                KeyId = result.KeyId.Value.ToString(),
                NewPrimaryVersion = result.NewPrimaryVersion,
                PublicKeyPem = result.PublicKey.Pem
            });
        }

        // GET /api/keys/{keyId}/public?version=1
        [HttpGet("{keyId}/public")]
        [ProducesResponseType(typeof(GetPublicKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GetPublicKeyResponseDto>> GetPublic(
            [FromRoute] string keyId,
            [FromQuery] int? version,
            CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));

            var cmd = new GetPublicKeyCommand(kid, version);

            var actor = GetActor();
            var requestId = HttpContext.TraceIdentifier;

            var result = await _getPublicKey.ExecuteAsync(cmd, actor, requestId);

            return Ok(new GetPublicKeyResponseDto
            {
                KeyId = result.KeyId.Value.ToString(),
                KeyVersion = result.KeyVersion,
                PublicKeyPem = result.PublicKey.Pem
            });
        }

        [HttpDelete("{keyId}")]
        public async Task<OkResult> Delete([FromRoute] string keyId, CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));

            var cmd = new DeleteKeyCommand(kid);
            var actor = GetActor();
            var requestId = HttpContext.TraceIdentifier;

            await _deleteKey.ExecuteAsync(cmd, actor, requestId);
            
            return Ok();
        }
        
        private static KeyPurpose ParsePurpose(string? purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose))
                return KeyPurpose.Sign;

            if (Enum.TryParse<KeyPurpose>(purpose, ignoreCase: true, out var p))
                return p;

            throw new DomainException($"Unknown key purpose '{purpose}'.");
        }

        private string GetActor()
        {
            // MVP: if you add auth later, use User.Identity / JWT claims.
            // For now, keep it simple.
            if (User?.Identity?.IsAuthenticated == true)
                return User.Identity!.Name ?? "authenticated-user";

            return "dev";
        }
    }
}
