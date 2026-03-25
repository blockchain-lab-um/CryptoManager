using System.Collections.Concurrent;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.Enums;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Development ICertificateAuthority that generates its own in-memory RSA-2048 CA key at
/// startup and issues certificates synchronously. Swap for a real CA implementation in
/// production by changing the DI registration only.
/// </summary>
public sealed class SoftSelfSignedCertificateAuthority : ICertificateAuthority
{
    private readonly ILogger<SoftSelfSignedCertificateAuthority> _logger;
    private readonly AsymmetricCipherKeyPair _caKeyPair;
    private readonly X509Certificate _caCert;
    private readonly byte[] _caCertDer;
    private readonly X509Name _caSubject;
    private readonly SecureRandom _random = new();

    private readonly ConcurrentDictionary<string, CertificateEnrollmentResult> _issued = new(StringComparer.Ordinal);

    public CertificateSource Source => CertificateSource.SelfSigned;

    public SoftSelfSignedCertificateAuthority(ILogger<SoftSelfSignedCertificateAuthority> logger)
    {
        _logger = logger;
        (_caKeyPair, _caCert, _caCertDer) = GenerateCa();
        _caSubject = _caCert.SubjectDN;
        _logger.LogInformation("SoftSelfSignedCertificateAuthority: CA ready, subject={Subject}", _caSubject);
    }

    public Task<string> SubmitCsrAsync(byte[] csrDer, CancellationToken ct = default)
    {
        var csr = new Pkcs10CertificationRequest(csrDer);

        if (!csr.Verify())
            throw new InvalidOperationException("CSR proof-of-possession signature verification failed.");

        var subjectPublicKey = csr.GetPublicKey();

        var serial = new BigInteger(128, _random);
        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter  = DateTime.UtcNow.AddDays(365);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(_caSubject);
        gen.SetSubjectDN(csr.GetCertificationRequestInfo().Subject);
        gen.SetNotBefore(notBefore);
        gen.SetNotAfter(notAfter);
        gen.SetPublicKey(subjectPublicKey);

        gen.AddExtension(X509Extensions.KeyUsage, critical: true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation));
        gen.AddExtension(X509Extensions.BasicConstraints, critical: true,
            new BasicConstraints(cA: false));

        var signer = new Asn1SignatureFactory("SHA256WITHRSA", _caKeyPair.Private, _random);
        var issuedCert = gen.Generate(signer);

        var certDer = issuedCert.GetEncoded();
        var enrollmentId = Guid.NewGuid().ToString("N");

        _issued[enrollmentId] = new CertificateEnrollmentResult(certDer, [_caCertDer]);

        _logger.LogInformation(
            "SoftSelfSignedCertificateAuthority: issued cert serial={Serial}, enrollmentId={EnrollmentId}",
            serial.ToString(16), enrollmentId);

        return Task.FromResult(enrollmentId);
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

    private static (AsymmetricCipherKeyPair keyPair, X509Certificate cert, byte[] certDer) GenerateCa()
    {
        var random = new SecureRandom();

        var keyGenParams = new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537), random, 2048, certainty: 80);
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(keyGenParams);
        var keyPair = keyGen.GenerateKeyPair();

        var subject = new X509Name("CN=SoftCA, O=CryptoManager Dev");
        var serial  = BigInteger.ProbablePrime(128, random);

        var gen = new X509V3CertificateGenerator();
        gen.SetSerialNumber(serial);
        gen.SetIssuerDN(subject);
        gen.SetSubjectDN(subject);
        gen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        gen.SetNotAfter(DateTime.UtcNow.AddYears(10));
        gen.SetPublicKey(keyPair.Public);

        gen.AddExtension(X509Extensions.BasicConstraints, critical: true,
            new BasicConstraints(cA: true));
        gen.AddExtension(X509Extensions.KeyUsage, critical: true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        var signer = new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private, random);
        var cert = gen.Generate(signer);

        return (keyPair, cert, cert.GetEncoded());
    }
}