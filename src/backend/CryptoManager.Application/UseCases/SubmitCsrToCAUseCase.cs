using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class SubmitCsrToCAUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly ICertificateRepository _certificateRepository;
    private readonly ICertificateAuthority _ca;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public SubmitCsrToCAUseCase(
        IKeyRepository keyRepository,
        ICertificateRepository certificateRepository,
        ICertificateAuthority ca,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _keyRepository         = keyRepository;
        _certificateRepository = certificateRepository;
        _ca                    = ca;
        _unitOfWork            = unitOfWork;
        _clock                 = clock;
        _currentUser           = currentUser;
    }

    public async Task<SubmitCsrToCAResult> ExecuteAsync(SubmitCsrToCACommand command, CancellationToken ct = default)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
            ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
            throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

        if (key.State != KeyState.Active)
            throw new DomainException($"Key '{key.Name}' is not active.");

        var keyVersion = key.GetPrimaryVersion();

        var enrollmentId = await _ca.SubmitCsrAsync(command.CsrDer, ct);

        var cert = Certificate.CreatePending(
            id:           CertificateId.New(),
            keyVersionId: keyVersion.Id,
            source:       _ca.Source,
            enrollmentId: enrollmentId,
            createdAt:    _clock.UtcNow,
            createdBy:    _currentUser.Actor);

        await _certificateRepository.AddAsync(cert, ct);
        await _unitOfWork.SaveAsync(ct);

        return new SubmitCsrToCAResult(cert.Id, enrollmentId);
    }
}