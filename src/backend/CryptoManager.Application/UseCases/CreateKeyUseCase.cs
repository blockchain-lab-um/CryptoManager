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
        private readonly ICurrentUser _currentUser;

        public CreateKeyUseCase(
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

        public async Task<CreateKeyResult> ExecuteAsync(CreateKeyCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
                throw new DomainException("Key name must not be empty.");

            if (command.AllowedMechanisms is null || command.AllowedMechanisms.Count == 0)
                throw new DomainException("At least one allowed mechanism must be configured.");

            var existing = await _keyRepository.GetByNameAndOwnerAsync(command.Name, _currentUser.UserId);
            if (existing is not null)
                throw new DomainException($"Key name '{command.Name}' already exists.");

            var primaryMechanism = command.AllowedMechanisms.First();
            var provider = _hsmRegistry.ResolveFirstAvailable();
            var keyId = KeyId.New();

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
                await WriteAuditAsync(success: false, error: ex.Message);
                throw;
            }

            var now = _clock.UtcNow;

            var key = new Key(
                id: keyId,
                name: command.Name,
                purpose: command.KeyPurpose,
                allowedMechanisms: command.AllowedMechanisms,
                createdAt: now,
                createdBy: _currentUser.Actor,
                ownerId: _currentUser.UserId
            );

            var v1 = key.AddVersion(
                versionId: KeyVersionId.New(),
                versionNumber: 1,
                providerRef: created.providerRef,
                publicKey: created.publicKey,
                createdAt: now,
                createdBy: _currentUser.Actor
            );

            await _keyRepository.AddAsync(key);
            await WriteAuditAsync(success: true);

            return new CreateKeyResult(
                KeyId: key.Id,
                Name: key.Name,
                KeyPurpose: key.Purpose,
                PrimaryVersion: v1.Version,
                PublicKey: v1.PublicKey
            );

            async Task WriteAuditAsync(bool success, string? error = null)
            {
                var evt = new AuditEvent(
                    AuditEventId.New(),
                    _clock.UtcNow,
                    _currentUser.Actor,
                    AuditAction.CreateKey,
                    keyId: success ? keyId : null,
                    keyVersion: success ? 1 : null,
                    mechanism: primaryMechanism,
                    requestId: null,
                    success: success,
                    error: error
                );
                await _auditSink.WriteAsync(evt);
            }
        }
    }
}