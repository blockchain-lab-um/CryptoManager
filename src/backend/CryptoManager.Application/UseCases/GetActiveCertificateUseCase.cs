using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class GetActiveCertificateUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly ICurrentUser _currentUser;

    public GetActiveCertificateUseCase(
        IKeyRepository keyRepository,
        ICertificateRepository certificateRepository,
        ICurrentUser currentUser)
    {
        _keyRepository         = keyRepository;
        _certificateRepository = certificateRepository;
        _currentUser           = currentUser;
    }

    public async Task<CertificateDto?> ExecuteAsync(KeyId keyId, CancellationToken ct = default)
    {
        var key = await _keyRepository.GetByIdAsync(keyId)
            ?? throw new NotFoundException($"Key '{keyId}' not found.");

        if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
            throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

        if (key.State != KeyState.Active)
            throw new DomainException($"Key '{key.Name}' is not active.");

        var keyVersion = key.GetPrimaryVersion();

        var cert = await _certificateRepository.GetActiveCertificateAsync(keyVersion.Id, ct);

        if (cert is null)
            return null;

        return new CertificateDto(
            Id:           cert.Id,
            KeyVersionId: cert.KeyVersionId,
            Status:       cert.Status,
            Source:       cert.Source,
            SerialNumber: cert.SerialNumber,
            Thumbprint:   cert.Thumbprint,
            SubjectDN:    cert.SubjectDN,
            IssuerDN:     cert.IssuerDN,
            NotBefore:    cert.NotBefore,
            NotAfter:     cert.NotAfter,
            EnrollmentId: cert.EnrollmentId,
            CreatedAt:    cert.CreatedAt,
            CreatedBy:    cert.CreatedBy);
    }
}