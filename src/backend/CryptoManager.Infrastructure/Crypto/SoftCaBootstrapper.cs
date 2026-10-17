using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class SoftCaBootstrapper : ISoftCaBootstrapper
{
    private const string SoftCaName = "SoftCA";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHsmProviderRegistry _hsmRegistry;
    private readonly IClock _clock;
    private readonly HsmBackedCertificateFactory _certificateFactory;
    private readonly ILogger<SoftCaBootstrapper> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SoftCaMaterial? _cached;

    public SoftCaBootstrapper(
        IServiceScopeFactory scopeFactory,
        IHsmProviderRegistry hsmRegistry,
        IClock clock,
        HsmBackedCertificateFactory certificateFactory,
        ILogger<SoftCaBootstrapper> logger)
    {
        _scopeFactory = scopeFactory;
        _hsmRegistry = hsmRegistry;
        _clock = clock;
        _certificateFactory = certificateFactory;
        _logger = logger;
    }

    public async Task<SoftCaMaterial> EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
            return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null)
                return _cached;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISystemCertificateAuthorityRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var validator = scope.ServiceProvider.GetRequiredService<ICertificateValidator>();

            var existing = await repo.GetActiveByNameAsync(SoftCaName, ct);
            if (existing is not null)
            {
                _cached = await ValidateExistingAsync(existing, validator, ct);
                _logger.LogInformation("Soft CA loaded: subject={Subject}, thumbprint={Thumbprint}", _cached.SubjectDn, _cached.Thumbprint);
                return _cached;
            }

            var provider = _hsmRegistry.ResolveFirstAvailable();
            var created = await provider.CreateSigningKeyAsync(SoftCaName, Mechanism.RsaPssSha256);
            created.ProviderRef.ProviderInstanceId = provider.InstanceId;

            var now = _clock.UtcNow;
            var cert = _certificateFactory.CreateSelfSignedCa(
                created.PublicKey.Pem,
                new X509Name("CN=SoftCA, O=CryptoManager Dev"),
                now.UtcDateTime.AddDays(-1),
                now.UtcDateTime.AddYears(10),
                BigInteger.ProbablePrime(128, new SecureRandom()),
                digest => provider.SignDigestAsync(created.ProviderRef, Mechanism.RsaPssSha256, digest));

            var certDer = cert.GetEncoded();
            var info = validator.ParseCertificateInfo(certDer);

            var entity = SystemCertificateAuthority.Create(
                SoftCaName,
                created.ProviderRef,
                certDer,
                info.SubjectDN,
                info.Thumbprint,
                info.SerialNumber,
                now);

            await repo.AddAsync(entity, ct);
            await unitOfWork.SaveAsync(ct);

            _cached = new SoftCaMaterial(entity.ProviderRef, entity.CertificateDer, entity.SubjectDn, entity.Thumbprint, entity.SerialNumber);
            _logger.LogInformation("Soft CA created: subject={Subject}, thumbprint={Thumbprint}", entity.SubjectDn, entity.Thumbprint);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SoftCaMaterial> ValidateExistingAsync(
        SystemCertificateAuthority existing,
        ICertificateValidator validator,
        CancellationToken ct)
    {
        var provider = _hsmRegistry.Resolve(existing.ProviderRef.ProviderInstanceId);
        var publicKey = await provider.GetPublicKeyAsync(existing.ProviderRef);
        if (!validator.VerifyMatchesPublicKey(existing.CertificateDer, publicKey.Pem))
            throw new InvalidOperationException($"Persisted Soft CA certificate does not match HSM key reference '{existing.ProviderRef.Reference}'.");

        return new SoftCaMaterial(
            existing.ProviderRef,
            existing.CertificateDer,
            existing.SubjectDn,
            existing.Thumbprint,
            existing.SerialNumber);
    }
}
