using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Application.Exceptions;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Application.UseCases;

public sealed class GenerateCsrUseCase
{
    private readonly IKeyRepository _keyRepository;
    private readonly IHsmProviderRegistry _hsmRegistry;
    private readonly ICsrBuilder _csrBuilder;
    private readonly ICurrentUser _currentUser;

    public GenerateCsrUseCase(
        IKeyRepository keyRepository,
        IHsmProviderRegistry hsmRegistry,
        ICsrBuilder csrBuilder,
        ICurrentUser currentUser)
    {
        _keyRepository = keyRepository;
        _hsmRegistry   = hsmRegistry;
        _csrBuilder    = csrBuilder;
        _currentUser   = currentUser;
    }

    public async Task<GenerateCsrResult> ExecuteAsync(GenerateCsrCommand command, CancellationToken ct = default)
    {
        var key = await _keyRepository.GetByIdAsync(command.KeyId)
            ?? throw new NotFoundException($"Key '{command.KeyId}' not found.");

        if (!key.IsOwnedBy(_currentUser.UserId) && !_currentUser.IsInRole("Admin"))
            throw new ForbiddenException($"You do not have access to key '{key.Name}'.");

        if (key.State != KeyState.Active)
            throw new DomainException($"Key '{key.Name}' is not active.");

        var keyVersion = key.GetPrimaryVersion();

        if (keyVersion.Status is KeyVersionStatus.Disabled or KeyVersionStatus.Destroyed)
            throw new DomainException($"Key version {keyVersion.Version} is not usable.");

        // Resolve the mechanism used to sign the CSR (first allowed mechanism = key's primary algorithm).
        var mechanism = Mechanism.Parse(key.AllowedMechanisms.First());

        var provider = _hsmRegistry.Resolve(keyVersion.ProviderRef.ProviderInstanceId);

        Func<byte[], Task<byte[]>> signDigestAsync =
            digest => provider.SignDigestAsync(keyVersion.ProviderRef, mechanism, digest);

        var csrDer = await _csrBuilder.BuildCsrAsync(
            keyVersion.PublicKey.Pem, command.Subject, signDigestAsync, ct);

        return new GenerateCsrResult(csrDer);
    }
}