using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public class DeleteKeyUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly IAuditSink _auditSink;
    private readonly IClock _clock;
    private readonly IHsmProvider _hsmProvider;

    public DeleteKeyUseCase(
        IKeyRepository keyRepository,
        IAuditSink auditSink,
        IClock clock,
        IHsmProvider hsmProvider)
    {
        _keyRepository = keyRepository;
        _auditSink = auditSink;
        _clock = clock;
        _hsmProvider = hsmProvider;
    }

    public async Task ExecuteAsync(
        DeleteKeyCommand command,
        string actor,
        string? requestId)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
                  ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        foreach (var version in key.Versions.Where(v => v.Status is KeyVersionStatus.Primary or KeyVersionStatus.Active))
        {
            await _hsmProvider.DestroyPrivateKeyAsync(version.ProviderRef);
        }

        key.Delete();
        await _keyRepository.DeleteAsync(key);

        var auditEvent = new AuditEvent(
            id: AuditEventId.New(),
            timestamp: _clock.UtcNow,
            actor: actor,
            action: AuditAction.DeleteKey,
            keyId: key.Id,
            keyVersion: null,
            mechanism: null,
            requestId,
            success: true,
            error: null
        );

        await _auditSink.WriteAsync(auditEvent);
    }
}