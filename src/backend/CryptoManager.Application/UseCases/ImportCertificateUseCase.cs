using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class ImportCertificateUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly ICertificateValidator _validator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditSink _auditSink;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public ImportCertificateUseCase(
        IKeyRepository keyRepository,
        ICertificateRepository certificateRepository,
        ICertificateValidator validator,
        IUnitOfWork unitOfWork,
        IAuditSink auditSink,
        IClock clock,
        ICurrentUser currentUser)
    {
        _keyRepository         = keyRepository;
        _certificateRepository = certificateRepository;
        _validator             = validator;
        _unitOfWork            = unitOfWork;
        _auditSink             = auditSink;
        _clock                 = clock;
        _currentUser           = currentUser;
    }

    public async Task<ImportCertificateResult> ExecuteAsync(ImportCertificateCommand command, CancellationToken ct = default)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
            ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
            throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

        if (key.State != KeyState.Active)
            throw new DomainException($"Key '{key.Name}' is not active.");

        var keyVersion = key.GetPrimaryVersion();

        // Verify the certificate's public key matches the key version's public key.
        if (!_validator.VerifyMatchesPublicKey(command.CertDer, keyVersion.PublicKey.Pem))
            throw new DomainException("The certificate's public key does not match the key version's public key.");

        var chainDer = command.ChainDer ?? [];

        // Validate chain if provided.
        if (chainDer.Length > 0)
        {
            var chainResult = _validator.ValidateChain(command.CertDer, chainDer, _clock.UtcNow);
            if (!chainResult.IsValid)
                throw new DomainException($"Certificate chain validation failed: {chainResult.Error}");
        }

        var info = _validator.ParseCertificateInfo(command.CertDer);

        // Check for thumbprint collision.
        var existing = await _certificateRepository.GetByThumbprintAsync(info.Thumbprint, ct);
        if (existing is not null)
            throw new DomainException($"A certificate with thumbprint '{info.Thumbprint}' already exists.");

        // Supersede any currently-active certificate for the same key version.
        var activeNow = await _certificateRepository.GetActiveCertificateAsync(keyVersion.Id, ct);
        if (activeNow is not null)
        {
            activeNow.Supersede();
            await _certificateRepository.UpdateAsync(activeNow, ct);
        }

        var cert = Certificate.CreateActive(
            id:             CertificateId.New(),
            keyVersionId:   keyVersion.Id,
            source:         CertificateSource.Imported,
            serialNumber:   info.SerialNumber,
            thumbprint:     info.Thumbprint,
            subjectDN:      info.SubjectDN,
            issuerDN:       info.IssuerDN,
            notBefore:      info.NotBefore,
            notAfter:       info.NotAfter,
            certificateDer: command.CertDer,
            chainDer:       chainDer.Length > 0 ? chainDer : null,
            createdAt:      _clock.UtcNow,
            createdBy:      _currentUser.Actor);

        await _certificateRepository.AddAsync(cert, ct);
        await _unitOfWork.SaveAsync(ct);

        await _auditSink.WriteAsync(new AuditEvent(
            AuditEventId.New(),
            _clock.UtcNow,
            _currentUser.Actor,
            _currentUser.UserId,
            AuditAction.ImportCertificate,
            keyId: key.Id,
            keyVersion: keyVersion.Version,
            mechanism: null,
            requestId: null,
            success: true,
            error: null,
            certificateId: cert.Id.Value,
            certificateThumbprint: info.Thumbprint));

        return new ImportCertificateResult(cert.Id, info.Thumbprint);
    }
}