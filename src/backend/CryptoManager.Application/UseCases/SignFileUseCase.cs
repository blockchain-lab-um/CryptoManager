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
    public sealed class SignFileUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly ISignedArtifactBuilder _artifactBuilder;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;

        public SignFileUseCase(
            IKeyRepository keyRepository,
            ISignedArtifactBuilder artifactBuilder,
            IAuditSink auditSink,
            IClock clock)
        {
            _keyRepository = keyRepository;
            _artifactBuilder = artifactBuilder;
            _auditSink = auditSink;
            _clock = clock;
        }

        public async Task<SignFileResult> ExecuteAsync(
            SignFileCommand command,
            string actor,
            string? requestId,
            CancellationToken ct = default)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

            if (key.State != KeyState.Active)
                throw new DomainException($"Key '{key.Name}' is not active.");

            KeyVersion keyVersion = key.GetPrimaryVersion();

            if (keyVersion.Status is KeyVersionStatus.Disabled or KeyVersionStatus.Destroyed)
                throw new DomainException($"Key version {keyVersion.Version} is not usable.");

            if (!key.IsMechanismAllowed(command.Mechanism))
                throw new DomainException(
                    $"Mechanism '{command.Mechanism.Name}' is not allowed for key '{key.Name}'.");

            SignedArtifact signed;
            try
            {
                signed = await _artifactBuilder.SignAsync(
                    keyVersion.ProviderRef,
                    command.Mechanism,
                    command.OriginalFileName,
                    command.FileBytes,
                    ct);
            }
            catch (Exception ex)
            {
                await WriteAuditAsync(success: false, error: ex.Message);
                throw;
            }

            var auditId = await WriteAuditAsync(success: true);

            return new SignFileResult(
                KeyId: key.Id,
                KeyVersion: keyVersion.Version,
                Mechanism: command.Mechanism,
                SignedFormat: signed.Format,
                OutputFileName: signed.OutputFileName,
                OutputContentType: signed.OutputContentType,
                SignedFileBytes: signed.Bytes,
                AuditEventId: auditId
            );

            async Task<AuditEventId> WriteAuditAsync(bool success, string? error = null)
            {
                // Add a new action if you want: AuditAction.SignFile
                var evt = new AuditEvent(
                    AuditEventId.New(),
                    _clock.UtcNow,
                    actor,
                    AuditAction.SignFile,
                    key.Id,
                    keyVersion.Version,
                    command.Mechanism,
                    requestId,
                    success,
                    error
                );

                await _auditSink.WriteAsync(evt);
                return evt.Id;
            }
        }
    }
}
