using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases
{
    public sealed class RotateKeyUseCase
    {
        private readonly IKeyRepository _keyRepository;
        private readonly IHsmProviderRegistry _hsmRegistry;
        private readonly IAuditSink _auditSink;
        private readonly IClock _clock;

        public RotateKeyUseCase(
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

        public async Task<RotateKeyResult> ExecuteAsync(
            RotateKeyCommand command,
            string actor,
            string? requestId)
        {
            var key = await _keyRepository.GetByIdAsync(command.KeyId)
                ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

            if (key.State != KeyState.Active)
                throw new DomainException($"Key '{key.Name}' is not active and cannot be rotated.");

            var currentKeyVersion = key.GetPrimaryVersion();

            var nextVersion = key.Versions.Count == 0 ? 1 : key.Versions.Max(v => v.Version) + 1;

            var primaryMechanismName = key.AllowedMechanisms.FirstOrDefault()
                ?? throw new DomainException($"Key '{key.Name}' has no allowed mechanisms configured.");

            var primaryMechanism = Mechanism.Parse(primaryMechanismName);

            var newProvider = _hsmRegistry.ResolveFirstAvailable();
            var oldProvider = _hsmRegistry.Resolve(currentKeyVersion.ProviderRef.ProviderInstanceId);

            (ProviderRef providerRef, PublicKeyMaterial publicKey) created;
            try
            {
                created = await newProvider.CreateSigningKeyAsync(
                    KeyName: key.Name,
                    Mechanism: primaryMechanism);
                created.providerRef.ProviderInstanceId = newProvider.InstanceId;

                if (oldProvider.IsAvailable())
                {
                    await oldProvider.DestroyPrivateKeyAsync(currentKeyVersion.ProviderRef);
                }
            }
            catch (Exception ex)
            {
                await WriteAuditAsync(success: false, error: ex.Message);
                throw;
            }

            var now = _clock.UtcNow;

            var newKv = key.AddVersion(
                versionId: KeyVersionId.New(),
                versionNumber: nextVersion,
                providerRef: created.providerRef,
                publicKey: created.publicKey,
                createdAt: now,
                createdBy: actor
            );

            key.PromoteVersionToPrimary(newKv.Version);
            key.RetireVersion(currentKeyVersion.Version);

            await _keyRepository.UpdateAsync(key);

            await WriteAuditAsync(success: true);

            return new RotateKeyResult(
                KeyId: key.Id,
                NewPrimaryVersion: newKv.Version,
                PublicKey: newKv.PublicKey
            );

            async Task WriteAuditAsync(bool success, string? error = null)
            {
                var evt = new AuditEvent(
                    AuditEventId.New(),
                    _clock.UtcNow,
                    actor,
                    AuditAction.RotateKey,
                    key.Id,
                    success ? nextVersion : null,
                    mechanism: primaryMechanism,
                    requestId: requestId,
                    success: success,
                    error: error
                );

                await _auditSink.WriteAsync(evt);
            }
        }
    }
}
