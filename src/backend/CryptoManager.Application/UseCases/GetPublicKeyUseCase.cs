using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Application.UseCases
{
    public class GetPublicKeyUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;

        public GetPublicKeyUseCase(
            IKeyRepository keyRepository,
            IAuditSink auditSink,
            IClock clock)
        {
            _keyRepository = keyRepository;
            _auditSink = auditSink;
            _clock = clock;
        }

        public async Task<GetPublicKeyResult> ExecuteAsync(
        GetPublicKeyCommand command,
        string actor,
        string? requestId)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

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
                throw new DomainException(
                    $"Key version {keyVersion.Version} is not usable.");

            var auditEvent = new AuditEvent(
                AuditEventId.New(),
                _clock.UtcNow,
                actor,
                AuditAction.GetPublicKey,
                key.Id,
                keyVersion.Version,
                mechanism: null,
                requestId,
                success: true,
                error: null
            );

            await _auditSink.WriteAsync(auditEvent);

            return new GetPublicKeyResult(
                key.Id,
                keyVersion.Version,
                keyVersion.PublicKey
            );
        }
    }
}
