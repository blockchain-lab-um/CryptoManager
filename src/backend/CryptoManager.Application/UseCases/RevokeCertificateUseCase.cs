using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class RevokeCertificateUseCase
{
    private readonly ICertificateRepository _certificateRepository;
    private readonly ICertificateAuthority _ca;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditSink _auditSink;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public RevokeCertificateUseCase(
        ICertificateRepository certificateRepository,
        ICertificateAuthority ca,
        IUnitOfWork unitOfWork,
        IAuditSink auditSink,
        IClock clock,
        ICurrentUser currentUser)
    {
        _certificateRepository = certificateRepository;
        _ca                    = ca;
        _unitOfWork            = unitOfWork;
        _auditSink             = auditSink;
        _clock                 = clock;
        _currentUser           = currentUser;
    }

    public async Task ExecuteAsync(RevokeCertificateCommand command, CancellationToken ct = default)
    {
        var cert = await _certificateRepository.GetByIdAsync(command.CertificateId, ct)
            ?? throw new NotFoundException($"Certificate '{command.CertificateId}' not found.");

        if (cert.Status is not (CertificateStatus.Active or CertificateStatus.Superseded))
            throw new DomainException($"Certificate with status '{cert.Status}' cannot be revoked.");

        // Notify the CA (no-op for soft CA, real call for production CA).
        if (cert.SerialNumber is not null)
            await _ca.RevokeCertificateAsync(cert.SerialNumber, command.Reason, ct);

        cert.Revoke();
        await _certificateRepository.UpdateAsync(cert, ct);
        await _unitOfWork.SaveAsync(ct);

        await _auditSink.WriteAsync(new AuditEvent(
            AuditEventId.New(),
            _clock.UtcNow,
            _currentUser.Actor,
            _currentUser.UserId,
            AuditAction.RevokeCertificate,
            keyId: null,
            keyVersion: null,
            mechanism: null,
            requestId: null,
            success: true,
            error: null,
            certificateId: cert.Id.Value,
            certificateThumbprint: cert.Thumbprint));
    }
}