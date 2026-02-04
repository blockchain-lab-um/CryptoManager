using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoManager.Infrastructure.HSM.SoftHSM
{
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

                var stored = StoredKey.ForRsa(rsa, publicPem, mechanism);
                _keys[providerRef.Reference] = stored;

                return Task.FromResult((providerRef, new PublicKeyMaterial(publicPem)));
            }

            if (mechanism.Name == Mechanism.EcdsaP256Sha256Der.Name)
            {
                var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

                var publicPem = Pem("PUBLIC KEY", ecdsa.ExportSubjectPublicKeyInfo());

                var stored = StoredKey.ForEcdsa(ecdsa, publicPem, mechanism);
                _keys[providerRef.Reference] = stored;

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

        public Task<PublicKeyMaterial> GetPublicKeyAsync(
            ProviderRef providerRef)
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

            private StoredKey(Mechanism mechanism, string publicKeyPem, RSA? rsa, ECDsa? ecdsa)
            {
                Mechanism = mechanism;
                PublicKeyPem = publicKeyPem;
                Rsa = rsa;
                Ecdsa = ecdsa;
            }

            public static StoredKey ForRsa(RSA rsa, string publicKeyPem, Mechanism mechanism) =>
                new(mechanism, publicKeyPem, rsa, null);

            public static StoredKey ForEcdsa(ECDsa ecdsa, string publicKeyPem, Mechanism mechanism) =>
                new(mechanism, publicKeyPem, null, ecdsa);
        }
    }

}
