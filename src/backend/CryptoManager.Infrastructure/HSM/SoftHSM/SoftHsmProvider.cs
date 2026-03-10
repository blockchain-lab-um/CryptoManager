using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CryptoManager.Infrastructure.HSM.SoftHSM;

public sealed class SoftHsmProvider : IHsmProvider
{
    private readonly ConcurrentDictionary<string, StoredKey> _keys = new(StringComparer.Ordinal);

    public Task<(ProviderRef ProviderRef, PublicKeyMaterial PublicKey)> CreateSigningKeyAsync(
        string keyName,
        Mechanism mechanism)
    {
        var id = Guid.NewGuid().ToString("N");
        var label = $"{keyName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var reference = $"softhsm:id={id};label={label};mech={mechanism.Name}";
        var providerRef = new ProviderRef("SoftHsm", reference);

        if (mechanism.Name == Mechanism.RsaPssSha256.Name)
        {
            var rsa = RSA.Create(2048);
            var publicPem = Pem("PUBLIC KEY", rsa.ExportSubjectPublicKeyInfo());

            var certReq = new CertificateRequest(
                new X500DistinguishedName($"CN=SoftHSM-{id}"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
            var cert = certReq.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));

            _keys[providerRef.Reference] = StoredKey.ForRsa(rsa, publicPem, mechanism, cert);
            return Task.FromResult((providerRef, new PublicKeyMaterial(publicPem)));
        }

        if (mechanism.Name == Mechanism.EcdsaP256Sha256Der.Name)
        {
            var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicPem = Pem("PUBLIC KEY", ecdsa.ExportSubjectPublicKeyInfo());

            var certReq = new CertificateRequest(
                new X500DistinguishedName($"CN=SoftHSM-{id}"),
                ecdsa,
                HashAlgorithmName.SHA256);
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
            var cert = certReq.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));

            _keys[providerRef.Reference] = StoredKey.ForEcdsa(ecdsa, publicPem, mechanism, cert);
            return Task.FromResult((providerRef, new PublicKeyMaterial(publicPem)));
        }

        throw new DomainException($"SoftHsmProvider does not support mechanism '{mechanism.Name}'.");
    }

    public Task<byte[]> SignDigestAsync(
        ProviderRef providerRef,
        Mechanism mechanism,
        byte[] digest)
    {
        Guard.NotNull(digest, nameof(digest));

        if (!_keys.TryGetValue(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        if (!string.Equals(stored.Mechanism.Name, mechanism.Name, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Mechanism mismatch. Key is '{stored.Mechanism.Name}', request is '{mechanism.Name}'.");

        ValidateDigestLength(digest, mechanism);

        if (mechanism.Name == Mechanism.RsaPssSha256.Name)
        {
            var sig = stored.Rsa!.SignHash(
                digest,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
            return Task.FromResult(sig);
        }

        if (mechanism.Name == Mechanism.EcdsaP256Sha256Der.Name)
        {
            var sig = stored.Ecdsa!.SignHash(digest);
            return Task.FromResult(sig);
        }

        throw new DomainException($"SoftHsmProvider does not support mechanism '{mechanism.Name}'.");
    }

    public Task<X509Certificate2> GetSigningCertificateAsync(ProviderRef providerRef)
    {
        if (!_keys.TryGetValue(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        return Task.FromResult(stored.Certificate);
    }

    public Task DestroyPrivateKeyAsync(ProviderRef providerRef)
    {
        if (!_keys.TryRemove(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        stored.Rsa?.Dispose();
        stored.Ecdsa?.Dispose();

        return Task.CompletedTask;
    }

    public Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef providerRef)
    {
        if (!_keys.TryGetValue(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        return Task.FromResult(new PublicKeyMaterial(stored.PublicKeyPem));
    }

    private static void ValidateDigestLength(byte[] digest, Mechanism mechanism)
    {
        if (string.Equals(mechanism.HashAlgorithm, "SHA256", StringComparison.OrdinalIgnoreCase) && digest.Length != 32)
            throw new DomainException("Invalid digest length for SHA-256 (expected 32 bytes).");
    }

    private static string Pem(string label, byte[] der)
    {
        var b64 = Convert.ToBase64String(der);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        for (int i = 0; i < b64.Length; i += 64)
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }

    private sealed class StoredKey
    {
        public Mechanism Mechanism { get; }
        public string PublicKeyPem { get; }
        public RSA? Rsa { get; }
        public ECDsa? Ecdsa { get; }
        public X509Certificate2 Certificate { get; }

        private StoredKey(Mechanism mechanism, string publicKeyPem, RSA? rsa, ECDsa? ecdsa, X509Certificate2 certificate)
        {
            Mechanism = mechanism;
            PublicKeyPem = publicKeyPem;
            Rsa = rsa;
            Ecdsa = ecdsa;
            Certificate = certificate;
        }

        public static StoredKey ForRsa(RSA rsa, string publicKeyPem, Mechanism mechanism, X509Certificate2 certificate) =>
            new(mechanism, publicKeyPem, rsa, null, certificate);

        public static StoredKey ForEcdsa(ECDsa ecdsa, string publicKeyPem, Mechanism mechanism, X509Certificate2 certificate) =>
            new(mechanism, publicKeyPem, null, ecdsa, certificate);
    }
}
