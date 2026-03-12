using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases
{
    public class GetPublicKeyUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;

        public GetPublicKeyUseCase(
            IKeyRepository keyRepository,
            IAuditSink auditSink,
            IClock clock,
            ICurrentUser currentUser)
        {
            _keyRepository = keyRepository;
            _auditSink = auditSink;
            _clock = clock;
            _currentUser = currentUser;
        }

        public async Task<GetPublicKeyResult> ExecuteAsync(GetPublicKeyCommand command)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

            if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
                throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

            KeyVersion keyVersion;
            if (command.KeyVersion is null)
            {
                keyVersion = key.GetPrimaryVersion();
            }
            else
            {
                keyVersion = key.Versions.FirstOrDefault(v => v.Version == command.KeyVersion)
                    ?? throw new DomainException(
                        $"Key version {command.KeyVersion} not found for key '{key.Name}'.");
            }

            if (keyVersion.Status is KeyVersionStatus.Disabled or KeyVersionStatus.Destroyed)
                throw new DomainException($"Key version {keyVersion.Version} is not usable.");

            var auditEvent = new AuditEvent(
                AuditEventId.New(),
                _clock.UtcNow,
                _currentUser.Actor,
                AuditAction.GetPublicKey,
                key.Id,
                keyVersion.Version,
                mechanism: null,
                null,
                success: true,
                error: null
            );

            await _auditSink.WriteAsync(auditEvent);

            return new GetPublicKeyResult(key.Id, keyVersion.Version, keyVersion.PublicKey);
        }
    }
}