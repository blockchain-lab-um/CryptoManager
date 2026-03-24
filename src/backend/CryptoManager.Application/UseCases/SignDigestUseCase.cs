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
        private readonly IHsmProviderRegistry _hsmRegistry;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;

        public SignDigestUseCase(
            IKeyRepository keyRepository,
            IHsmProviderRegistry hsmRegistry,
            IAuditSink auditSink,
            IClock clock,
            ICurrentUser currentUser)
        {
            _keyRepository = keyRepository;
            _hsmRegistry = hsmRegistry;
            _auditSink = auditSink;
            _clock = clock;
            _currentUser = currentUser;
        }

        public async Task<SignDigestResult> ExecuteAsync(SignDigestCommand command)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

            // Declared outside the validation block so WriteAuditAsync can reference it
            // even when an exception is thrown before the version is resolved (null = unknown).
            KeyVersion? keyVersion = null;
            try
            {
                if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
                    throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

                if (key.State != KeyState.Active)
                    throw new DomainException($"Key '{key.Name}' is not active.");

                keyVersion = key.GetPrimaryVersion();

                if (keyVersion.Status is KeyVersionStatus.Disabled or KeyVersionStatus.Destroyed)
                    throw new DomainException($"Key version {keyVersion.Version} is not usable.");

                if (!key.IsMechanismAllowed(command.Mechanism))
                    throw new DomainException(
                        $"Mechanism '{command.Mechanism.Name}' is not allowed for key '{key.Name}'.");

                ValidateDigestLength(command.Digest, command.Mechanism);
            }
            catch (ForbiddenException ex) { await WriteAuditAsync(success: false, error: ex.Message); throw; }
            catch (DomainException ex)    { await WriteAuditAsync(success: false, error: ex.Message); throw; }

            var provider = _hsmRegistry.Resolve(keyVersion!.ProviderRef.ProviderInstanceId);

            byte[] signature;
            try
            {
                signature = await provider.SignDigestAsync(
                    keyVersion.ProviderRef,
                    command.Mechanism,
                    command.Digest);
            }
            catch (Exception ex)
            {
                await WriteAuditAsync(success: false, error: ex.Message);
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
                    _currentUser.Actor,
                    _currentUser.UserId,
                    AuditAction.Sign,
                    key.Id,
                    keyVersion?.Version,
                    command.Mechanism,
                    null,
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