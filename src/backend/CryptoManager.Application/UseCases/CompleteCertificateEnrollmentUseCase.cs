using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class CompleteCertificateEnrollmentUseCase
{
    private readonly ICertificateRepository _certificateRepository;
    private readonly ICertificateAuthority _ca;
    private readonly ICertificateValidator _validator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditSink _auditSink;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public CompleteCertificateEnrollmentUseCase(
        ICertificateRepository certificateRepository,
        ICertificateAuthority ca,
        ICertificateValidator validator,
        IUnitOfWork unitOfWork,
        IAuditSink auditSink,
        IClock clock,
        ICurrentUser currentUser)
    {
        _certificateRepository = certificateRepository;
        _ca                    = ca;
        _validator             = validator;
        _unitOfWork            = unitOfWork;
        _auditSink             = auditSink;
        _clock                 = clock;
        _currentUser           = currentUser;
    }

    public async Task<CompleteCertificateEnrollmentResult> ExecuteAsync(
        CompleteCertificateEnrollmentCommand command,
        CancellationToken ct = default)
    {
        var cert = await _certificateRepository.GetByEnrollmentIdAsync(command.EnrollmentId, ct)
            ?? throw new NotFoundException($"No certificate enrollment found for id '{command.EnrollmentId}'.");

        if (cert.Status != CertificateStatus.PendingEnrollment)
            throw new InvalidOperationException(
                $"Enrollment '{command.EnrollmentId}' is already in status '{cert.Status}'.");

        var result = await _ca.PollEnrollmentAsync(command.EnrollmentId, ct);

        if (result is null)
            return new CompleteCertificateEnrollmentResult(IsComplete: false);

        var info = _validator.ParseCertificateInfo(result.CertDer);

        // Supersede any currently-active certificate for the same key version.
        var existing = await _certificateRepository.GetActiveCertificateAsync(cert.KeyVersionId, ct);
        if (existing is not null)
        {
            existing.Supersede();
            await _certificateRepository.UpdateAsync(existing, ct);
        }

        cert.Activate(
            serialNumber:   info.SerialNumber,
            thumbprint:     info.Thumbprint,
            subjectDN:      info.SubjectDN,
            issuerDN:       info.IssuerDN,
            notBefore:      info.NotBefore,
            notAfter:       info.NotAfter,
            certificateDer: result.CertDer,
            chainDer:       result.ChainDer.Length > 0 ? result.ChainDer : null);

        await _certificateRepository.UpdateAsync(cert, ct);
        await _unitOfWork.SaveAsync(ct);

        await _auditSink.WriteAsync(new AuditEvent(
            AuditEventId.New(),
            _clock.UtcNow,
            _currentUser.Actor,
            _currentUser.UserId,
            AuditAction.IssueCertificate,
            keyId: null,
            keyVersion: null,
            mechanism: null,
            requestId: null,
            success: true,
            error: null,
            certificateId: cert.Id.Value,
            certificateThumbprint: info.Thumbprint));

        return new CompleteCertificateEnrollmentResult(
            IsComplete:     true,
            CertificateId:  cert.Id,
            Thumbprint:     info.Thumbprint);
    }
}