using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class SignDocumentUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly IHsmProviderRegistry _hsmRegistry;
    private readonly ISignedArtifactBuilder _artifactBuilder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditSink _auditSink;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public SignDocumentUseCase(
        IKeyRepository keyRepository,
        ICertificateRepository certificateRepository,
        IHsmProviderRegistry hsmRegistry,
        ISignedArtifactBuilder artifactBuilder,
        IUnitOfWork unitOfWork,
        IAuditSink auditSink,
        IClock clock,
        ICurrentUser currentUser)
    {
        _keyRepository         = keyRepository;
        _certificateRepository = certificateRepository;
        _hsmRegistry           = hsmRegistry;
        _artifactBuilder       = artifactBuilder;
        _unitOfWork            = unitOfWork;
        _auditSink             = auditSink;
        _clock                 = clock;
        _currentUser           = currentUser;
    }

    public async Task<SignFileResult> ExecuteAsync(SignFileCommand command, CancellationToken ct = default)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
            ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        KeyVersion? keyVersion = null;
        Certificate? cert = null;

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

            if (command.Mechanism == Mechanism.EcdsaP256Sha256Der)
                throw new DomainException("ECDSA document signing is not yet supported. Use RSA_PSS_SHA256.");

            cert = await _certificateRepository.GetActiveCertificateAsync(keyVersion.Id, ct);

            if (cert is null)
                throw new DomainException(
                    $"Key '{key.Name}' has no active certificate. Enroll or import one before signing documents.");

            // Lazy expiry: detect and persist the transition before rejecting.
            if (cert.NotAfter.HasValue && cert.NotAfter.Value < _clock.UtcNow)
            {
                cert.MarkExpired();
                await _certificateRepository.UpdateAsync(cert, ct);
                await _unitOfWork.SaveAsync(ct);

                await _auditSink.WriteAsync(new AuditEvent(
                    AuditEventId.New(),
                    _clock.UtcNow,
                    _currentUser.Actor,
                    _currentUser.UserId,
                    AuditAction.ExpireCertificate,
                    keyId: key.Id,
                    keyVersion: keyVersion.Version,
                    mechanism: null,
                    requestId: null,
                    success: true,
                    error: null,
                    certificateId: cert.Id.Value,
                    certificateThumbprint: cert.Thumbprint));

                throw new DomainException(
                    $"The active certificate for key '{key.Name}' has expired (NotAfter: {cert.NotAfter:O}). Renew the certificate before signing.");
            }
        }
        catch (ForbiddenException ex) { await WriteAuditAsync(success: false, error: ex.Message); throw; }
        catch (DomainException ex)    { await WriteAuditAsync(success: false, error: ex.Message); throw; }

        SignedArtifact signed;
        try
        {
            var provider = _hsmRegistry.Resolve(keyVersion.ProviderRef.ProviderInstanceId);

            var material = new DocumentSigningMaterial(
                Mechanism:        command.Mechanism,
                CertBundle:       new RawCertBundle(cert.CertificateDer!, cert.ChainDer ?? []),
                SignDigestAsync:  digest => provider.SignDigestAsync(keyVersion.ProviderRef, command.Mechanism, digest),
                RequestedBy:      _currentUser.Actor,
                SignedAt:         _clock.UtcNow);

            signed = await _artifactBuilder.SignAsync(
                command.OriginalFileName, command.FileBytes, material, command.Stamp, ct);
        }
        catch (Exception ex)
        {
            await WriteAuditAsync(success: false, error: ex.Message);
            throw;
        }

        var auditId = await WriteAuditAsync(success: true);

        return new SignFileResult(
            KeyId:            key.Id,
            KeyVersion:       keyVersion.Version,
            Mechanism:        command.Mechanism,
            SignedFormat:     signed.Format,
            OutputFileName:   signed.OutputFileName,
            OutputContentType: signed.OutputContentType,
            SignedFileBytes:  signed.Bytes,
            AuditEventId:     auditId);

        async Task<AuditEventId> WriteAuditAsync(bool success, string? error = null)
        {
            var evt = new AuditEvent(
                AuditEventId.New(),
                _clock.UtcNow,
                _currentUser.Actor,
                _currentUser.UserId,
                AuditAction.SignFile,
                keyId:                key.Id,
                keyVersion:           keyVersion?.Version,
                mechanism:            command.Mechanism,
                requestId:            null,
                success:              success,
                error:                error,
                certificateId:        cert?.Id.Value,
                certificateThumbprint: cert?.Thumbprint);
            await _auditSink.WriteAsync(evt);
            return evt.Id;
        }
    }
}
