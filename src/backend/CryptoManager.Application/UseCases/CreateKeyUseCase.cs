using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases
{
    public sealed class CreateKeyUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IHsmProviderRegistry _hsmRegistry;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;

        public CreateKeyUseCase(
            IKeyRepository keyRepository,
            IHsmProviderRegistry hsmRegistry,
            IAuditSink auditSink,
            IClock clock)
        {
            _keyRepository = keyRepository;
            _hsmRegistry = hsmRegistry;
            _auditSink = auditSink;
            _clock = clock;
        }

        public async Task<CreateKeyResult> ExecuteAsync(
            CreateKeyCommand command,
            string actor,
            string? requestId)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
                throw new DomainException("Key name must not be empty.");

            if (command.AllowedMechanisms is null || command.AllowedMechanisms.Count == 0)
                throw new DomainException("At least one allowed mechanism must be configured.");

            var existing = await _keyRepository.GetByNameAsync(command.Name);
            if (existing is not null)
                throw new DomainException($"Key name '{command.Name}' already exists.");

            
            var primaryMechanism = command.AllowedMechanisms.First();

            var provider = _hsmRegistry.ResolveFirstAvailable();

            (ProviderRef providerRef, PublicKeyMaterial publicKey) created;
            try
            {
                created = await provider.CreateSigningKeyAsync(
                    KeyName: command.Name,
                    Mechanism: primaryMechanism);
                created.providerRef.ProviderInstanceId = provider.InstanceId;
            }
            catch (Exception ex)
            {
                await WriteAuditAsync(success: false, actor: actor, error: ex.Message);
                throw;
            }

            var now = _clock.UtcNow;
            var keyId = KeyId.New();

            var key = new Key(
                id: keyId,
                name: command.Name,
                purpose: command.KeyPurpose,
                allowedMechanisms: command.AllowedMechanisms,
                createdAt: now,
                createdBy: actor
            );

            var v1 = key.AddVersion(
                versionId: KeyVersionId.New(),
                versionNumber: 1,
                providerRef: created.providerRef,
                publicKey: created.publicKey,
                createdAt: now,
                createdBy: actor
            );

            await _keyRepository.AddAsync(key);

            await WriteAuditAsync(success: true, actor: actor);

            return new CreateKeyResult(
                KeyId: key.Id,
                Name: key.Name,
                KeyPurpose: key.Purpose,
                PrimaryVersion: v1.Version,
                PublicKey: v1.PublicKey
            );
        }

        private async Task WriteAuditAsync(bool success, string actor, Mechanism? primaryMechanism = null, string? requestId = null, KeyId? keyId = null, string? error = null)
        {
            var evt = new AuditEvent(
                AuditEventId.New(),
                _clock.UtcNow,
                actor,
                AuditAction.CreateKey,
                keyId: keyId?.Value == Guid.Empty ? null : keyId,
                keyVersion: success ? 1 : null,
                mechanism: primaryMechanism,
                requestId: requestId,
                success: success,
                error: error
            );

            await _auditSink.WriteAsync(evt);
        }
    }
}
