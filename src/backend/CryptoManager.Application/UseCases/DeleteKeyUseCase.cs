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
    private readonly IHsmProviderRegistry _hsmRegistry;
    private readonly ICurrentUser _currentUser;

    public DeleteKeyUseCase(
        IKeyRepository keyRepository,
        IAuditSink auditSink,
        IClock clock,
        IHsmProviderRegistry hsmRegistry,
        ICurrentUser currentUser)
    {
        _keyRepository = keyRepository;
        _auditSink = auditSink;
        _clock = clock;
        _hsmRegistry = hsmRegistry;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(DeleteKeyCommand command)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
                  ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        foreach (var version in key.Versions.Where(v => v.Status is KeyVersionStatus.Primary or KeyVersionStatus.Active))
        {
            var provider = _hsmRegistry.Resolve(version.ProviderRef.ProviderInstanceId);
            await provider.DestroyPrivateKeyAsync(version.ProviderRef);
        }

        key.Delete();
        await _keyRepository.DeleteAsync(key);

        var auditEvent = new AuditEvent(
            id: AuditEventId.New(),
            timestamp: _clock.UtcNow,
            actor: _currentUser.Actor,
            action: AuditAction.DeleteKey,
            keyId: key.Id,
            keyVersion: null,
            mechanism: null,
            requestId: null,
            success: true,
            error: null
        );

        await _auditSink.WriteAsync(auditEvent);
    }
}