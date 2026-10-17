using CryptoManager.API.DTOs.Audit;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.UseCases;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoManager.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly GetAuditLogsUseCase _getAuditLogs;

    public AuditController(GetAuditLogsUseCase getAuditLogs)
    {
        _getAuditLogs = getAuditLogs;
    }

    // GET /api/Audit/logs
    [HttpGet("logs")]
    [Authorize(Policy = "CanOperate")]
    [ProducesResponseType(typeof(AuditLogResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogResponseDto>> GetLogs(
        [FromQuery] GetAuditLogsRequestDto request,
        CancellationToken ct)
    {
        AuditAction? action = null;
        if (request.Action is not null)
        {
            if (!Enum.TryParse<AuditAction>(request.Action, ignoreCase: true, out var parsed))
                throw new DomainException($"Unknown audit action '{request.Action}'.");
            action = parsed;
        }

        KeyId? keyId = null;
        if (request.KeyId is not null)
        {
            if (!Guid.TryParse(request.KeyId, out var keyGuid))
                throw new DomainException($"Invalid key ID '{request.KeyId}'.");
            keyId = new KeyId(keyGuid);
        }

        var command = new GetAuditLogsCommand(
            Actor: request.Actor,
            Action: action,
            KeyId: keyId,
            From: request.From,
            To: request.To,
            Page: request.Page,
            PageSize: request.PageSize);

        var result = await _getAuditLogs.ExecuteAsync(command, ct);

        return Ok(new AuditLogResponseDto
        {
            Items = result.Items.Select(e => new AuditLogEntryDto
            {
                Id = e.Id.ToString(),
                Timestamp = e.Timestamp,
                Actor = e.Actor,
                ActorId = e.ActorId,
                Action = e.Action.ToString(),
                KeyId = e.KeyId?.ToString(),
                KeyName = e.KeyName,
                KeyVersion = e.KeyVersion,
                Mechanism = e.Mechanism,
                Success = e.Success,
                Error = e.Error
            }).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize)
        });
    }
}