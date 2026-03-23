using CryptoManager.API.DTOs.Keys;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
        [Authorize(Policy = "CanOperate")]
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
                    CreatedAt = k.CreatedAt,
                    Owner = k.Owner
                }).ToList()
            });
        }

        // POST /api/keys
        [HttpPost]
        [Authorize(Policy = "CanOperate")]
        [ProducesResponseType(typeof(CreateKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<CreateKeyResponseDto>> Create(
            [FromBody] CreateKeyRequestDto request,
            CancellationToken ct)
        {
            var purpose = ParsePurpose(request.Purpose);
            var allowedMechanisms = request.AllowedMechanisms.Select(Mechanism.Parse).ToArray();

            var cmd = new CreateKeyCommand(
                Name: request.Name.Trim(),
                KeyPurpose: purpose,
                AllowedMechanisms: allowedMechanisms
            );

            var result = await _createKey.ExecuteAsync(cmd);

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
        [Authorize(Policy = "CanOperate")]
        [ProducesResponseType(typeof(RotateKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<RotateKeyResponseDto>> Rotate(
            [FromRoute] string keyId,
            CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));
            var result = await _rotateKey.ExecuteAsync(new RotateKeyCommand(kid));

            return Ok(new RotateKeyResponseDto
            {
                KeyId = result.KeyId.Value.ToString(),
                NewPrimaryVersion = result.NewPrimaryVersion,
                PublicKeyPem = result.PublicKey.Pem
            });
        }

        // GET /api/keys/{keyId}/public?version=1
        [HttpGet("{keyId}/public")]
        [Authorize(Policy = "CanOperate")]
        [ProducesResponseType(typeof(GetPublicKeyResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GetPublicKeyResponseDto>> GetPublic(
            [FromRoute] string keyId,
            [FromQuery] int? version,
            CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));
            var result = await _getPublicKey.ExecuteAsync(new GetPublicKeyCommand(kid, version));

            return Ok(new GetPublicKeyResponseDto
            {
                KeyId = result.KeyId.Value.ToString(),
                KeyVersion = result.KeyVersion,
                PublicKeyPem = result.PublicKey.Pem
            });
        }

        [HttpDelete("{keyId}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<OkResult> Delete([FromRoute] string keyId, CancellationToken ct)
        {
            var kid = new KeyId(Guid.Parse(keyId));
            await _deleteKey.ExecuteAsync(new DeleteKeyCommand(kid));
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
    }
}