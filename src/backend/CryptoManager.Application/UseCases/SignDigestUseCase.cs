using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases
{
    public sealed class SignDigestUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IHsmProvider _hsmProvider;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;

        public SignDigestUseCase(
            IKeyRepository keyRepository,
            IHsmProvider hsmProvider,
            IAuditSink auditSink,
            IClock clock)
        {
            _keyRepository = keyRepository;
            _hsmProvider = hsmProvider;
            _auditSink = auditSink;
            _clock = clock;
        }

        public async Task<SignDigestResult> ExecuteAsync(
            SignDigestCommand command,
            string actor,
            string? requestId)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

            if (key.State != KeyState.Active)
                throw new DomainException($"Key '{key.Name}' is not active.");

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

            if (!key.IsMechanismAllowed(command.Mechanism))
                throw new DomainException(
                    $"Mechanism '{command.Mechanism.Name}' is not allowed for key '{key.Name}'.");

            ValidateDigestLength(command.Digest, command.Mechanism);

            byte[] signature;
            try
            {
                signature = await _hsmProvider.SignDigestAsync(
                    keyVersion.ProviderRef,
                    command.Mechanism,
                    command.Digest);
            }
            catch (Exception ex)
            {
                await WriteAuditAsync(
                    success: false,
                    error: ex.Message);
                throw;
            }

            var auditId = await WriteAuditAsync(success: true);

            return new SignDigestResult(
                KeyId: key.Id,
                KeyVersion: keyVersion.Version,
                Mechanism: command.Mechanism,
                SignatureEncoding: command.Mechanism.SignatureEncoding,
                Signature: signature,
                AuditEventId: auditId
            );

            async Task<AuditEventId> WriteAuditAsync(bool success, string? error = null)
            {
                var evt = new AuditEvent(
                    AuditEventId.New(),
                    _clock.UtcNow,
                    actor,
                    AuditAction.Sign,
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

        private static void ValidateDigestLength(byte[] digest, Mechanism mechanism)
        {
            if (mechanism.HashAlgorithm == "SHA256" && digest.Length != 32)
                throw new DomainException("Invalid digest length for SHA-256.");
        }
    }

}