using System.Collections.Concurrent;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Development ICertificateAuthority backed by a single persisted HSM-held CA key.
/// The CA is bootstrapped once at startup and reused across restarts.
/// </summary>
public sealed class SoftSelfSignedCertificateAuthority : ICertificateAuthority
{
    private readonly ISoftCaBootstrapper _bootstrapper;
    private readonly IHsmProviderRegistry _hsmRegistry;
    private readonly HsmBackedCertificateFactory _certificateFactory;
    private readonly ILogger<SoftSelfSignedCertificateAuthority> _logger;
    private readonly SecureRandom _random = new();

    private readonly ConcurrentDictionary<string, CertificateEnrollmentResult> _issued = new(StringComparer.Ordinal);

    public CertificateSource Source => CertificateSource.SelfSigned;

    public SoftSelfSignedCertificateAuthority(
        ISoftCaBootstrapper bootstrapper,
        IHsmProviderRegistry hsmRegistry,
        HsmBackedCertificateFactory certificateFactory,
        ILogger<SoftSelfSignedCertificateAuthority> logger)
    {
        _bootstrapper = bootstrapper;
        _hsmRegistry = hsmRegistry;
        _certificateFactory = certificateFactory;
        _logger = logger;
    }

    public async Task<string> SubmitCsrAsync(byte[] csrDer, CancellationToken ct = default)
    {
        var csr = new Pkcs10CertificationRequest(csrDer);

        if (!csr.Verify())
            throw new InvalidOperationException("CSR proof-of-possession signature verification failed.");

        var ca = await _bootstrapper.EnsureInitializedAsync(ct);
        var caCert = new X509CertificateParser().ReadCertificate(ca.CertificateDer);
        var provider = _hsmRegistry.Resolve(ca.ProviderRef.ProviderInstanceId);

        var serial = new BigInteger(128, _random);
        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter  = DateTime.UtcNow.AddDays(365);

        var issuedCert = _certificateFactory.CreateIssuedCertificate(
            csr,
            caCert,
            notBefore,
            notAfter,
            serial,
            digest => provider.SignDigestAsync(ca.ProviderRef, Mechanism.RsaPssSha256, digest));

        var certDer = issuedCert.GetEncoded();
        var enrollmentId = Guid.NewGuid().ToString("N");

        _issued[enrollmentId] = new CertificateEnrollmentResult(certDer, [ca.CertificateDer]);

        _logger.LogInformation(
            "SoftSelfSignedCertificateAuthority: issued cert serial={Serial}, enrollmentId={EnrollmentId}",
            serial.ToString(16), enrollmentId);

        return enrollmentId;
    }

    public Task<CertificateEnrollmentResult?> PollEnrollmentAsync(string enrollmentId, CancellationToken ct = default)
    {
        _issued.TryGetValue(enrollmentId, out var result);
        return Task.FromResult(result);
    }

    public Task RevokeCertificateAsync(string serialNumber, RevocationReason reason, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "SoftSelfSignedCertificateAuthority: revocation no-op (serial={Serial}, reason={Reason})",
            serialNumber, reason);
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------------
}
