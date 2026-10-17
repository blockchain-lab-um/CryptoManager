using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoManager.Infrastructure.HSM.SoftHSM;

public sealed class SoftHsmProvider : IHsmProvider
{
    private readonly ConcurrentDictionary<string, StoredKey> _keys = new(StringComparer.Ordinal);
    private readonly string? _filePath;
    private readonly object _fileLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string InstanceId { get; }

    public SoftHsmProvider(string instanceId, string? filePath = null)
    {
        InstanceId = instanceId;
        _filePath = filePath;
        if (_filePath is not null)
            LoadFromDisk();
    }

    public bool IsAvailable() => true;

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

            _keys[providerRef.Reference] = StoredKey.ForRsa(rsa, publicPem, mechanism);
            PersistToDisk();
            return Task.FromResult((providerRef, new PublicKeyMaterial(publicPem)));
        }

        if (mechanism.Name == Mechanism.EcdsaP256Sha256Der.Name)
        {
            var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var publicPem = Pem("PUBLIC KEY", ecdsa.ExportSubjectPublicKeyInfo());

            _keys[providerRef.Reference] = StoredKey.ForEcdsa(ecdsa, publicPem, mechanism);
            PersistToDisk();
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

    public Task DestroyPrivateKeyAsync(ProviderRef providerRef)
    {
        if (!_keys.TryRemove(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        stored.Rsa?.Dispose();
        stored.Ecdsa?.Dispose();

        PersistToDisk();
        return Task.CompletedTask;
    }

    public Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef providerRef)
    {
        if (!_keys.TryGetValue(providerRef.Reference, out var stored))
            throw new DomainException("Key not found for ProviderRef.");

        return Task.FromResult(new PublicKeyMaterial(stored.PublicKeyPem));
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath)) return;

        var json = File.ReadAllText(_filePath!);
        var entries = JsonSerializer.Deserialize<List<KeyFileEntry>>(json) ?? [];

        foreach (var entry in entries)
        {
            var mechanism = Mechanism.Parse(entry.MechanismName);

            StoredKey stored;
            if (mechanism.Name == Mechanism.RsaPssSha256.Name)
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(entry.PrivateKeyPkcs8Pem);
                stored = StoredKey.ForRsa(rsa, entry.PublicKeyPem, mechanism);
            }
            else
            {
                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(entry.PrivateKeyPkcs8Pem);
                stored = StoredKey.ForEcdsa(ecdsa, entry.PublicKeyPem, mechanism);
            }

            _keys[entry.Reference] = stored;
        }
    }

    private void PersistToDisk()
    {
        if (_filePath is null) return;

        var entries = _keys.Select(kvp =>
        {
            var stored = kvp.Value;
            var privateKeyPem = stored.Rsa is not null
                ? stored.Rsa.ExportPkcs8PrivateKeyPem()
                : stored.Ecdsa!.ExportPkcs8PrivateKeyPem();

            return new KeyFileEntry(
                Reference: kvp.Key,
                MechanismName: stored.Mechanism.Name,
                PublicKeyPem: stored.PublicKeyPem,
                PrivateKeyPkcs8Pem: privateKeyPem);
        }).ToList();

        var json = JsonSerializer.Serialize(entries, JsonOptions);

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        lock (_fileLock)
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath!, overwrite: true);
        }
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

    private sealed record KeyFileEntry(
        string Reference,
        string MechanismName,
        string PublicKeyPem,
        string PrivateKeyPkcs8Pem);

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