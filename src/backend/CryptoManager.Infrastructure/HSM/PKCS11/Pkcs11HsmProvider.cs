using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.HighLevelAPI80.MechanismParams;
using System.Security.Cryptography;
using System.Text;
namespace CryptoManager.Infrastructure.HSM.PKCS11
{
    public sealed class Pkcs11HsmProvider : IHsmProvider, IDisposable
    {
        private readonly Pkcs11Options _opt;
        private readonly IPkcs11Library _pkcs11;

        public string InstanceId { get; }

        public Pkcs11HsmProvider(string instanceId, Pkcs11Options opt)
        {
            InstanceId = instanceId;
            _opt = opt;
            Pkcs11InteropFactories factories = new Pkcs11InteropFactories();
            _pkcs11 = new Pkcs11InteropFactories().Pkcs11LibraryFactory.LoadPkcs11Library(factories, opt.LibraryPath, AppType.SingleThreaded);
        }

        public void Dispose() => _pkcs11.Dispose();

        public bool IsAvailable()
        {
            try
            {
                var slots = _pkcs11.GetSlotList(SlotsType.WithTokenPresent);
                return slots.Any(s =>
                {
                    var label = (s.GetTokenInfo().Label ?? string.Empty).Trim();
                    return string.Equals(label, _opt.TokenLabel, StringComparison.Ordinal);
                });
            }
            catch
            {
                return false;
            }
        }

        public Task<(ProviderRef ProviderRef, PublicKeyMaterial PublicKey)> CreateSigningKeyAsync(
            string KeyName,
            Mechanism Mechanism)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(KeyName))
                    throw new DomainException("KeyName must not be empty.");

                // only RSA-PSS-SHA256 for now
                if (!string.Equals(Mechanism.Name, "RSA_PSS_SHA256", StringComparison.OrdinalIgnoreCase))
                    throw new DomainException($"Unsupported mechanism '{Mechanism.Name}'. MVP supports only RSA_PSS_SHA256.");

                var slot = FindSlotByTokenLabel(_pkcs11, _opt.TokenLabel);

                using var session = slot.OpenSession(SessionType.ReadWrite);
                session.Login(CKU.CKU_USER, _opt.UserPin);

                var ckaId = RandomNumberGenerator.GetBytes(16);
                var label = $"{KeyName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

                var pubTemplate = new List<IObjectAttribute>
                {
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, CKK.CKK_RSA),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_PRIVATE, false),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, label),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, ckaId),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_VERIFY, true),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_MODULUS_BITS, (ulong)_opt.RsaKeySizeBits),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_PUBLIC_EXPONENT, new byte[] { 0x01, 0x00, 0x01 }) // 65537
                };

                var privTemplate = new List<IObjectAttribute>
                {
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, CKK.CKK_RSA),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_PRIVATE, true),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, label),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, ckaId),

                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SIGN, true),

                    // Strong defaults (device-dependent; YubiHSM should enforce non-extractability anyway)
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SENSITIVE, true),
                    session.Factories.ObjectAttributeFactory.Create(CKA.CKA_EXTRACTABLE, false)
                };

                var genMech = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS_KEY_PAIR_GEN);

                session.GenerateKeyPair(genMech, pubTemplate, privTemplate, out var pubHandle, out var privHandle);

                // Export public key in PEM (SubjectPublicKeyInfo)
                var publicPem = ExportRsaPublicKeyPem(session, pubHandle);

                session.Logout();

                var pref = new ProviderRef(
                    providerType: "PKCS11",
                    reference: $"token={_opt.TokenLabel};id={Convert.ToHexString(ckaId)};label={label}"
                );

                return (pref, new PublicKeyMaterial(publicPem));
            });
        }

        public Task DestroyPrivateKeyAsync(ProviderRef providerRef)
        {
            return Task.Run(() =>
            {
                var info = ParseProviderRef(providerRef);

                var slot = FindSlotByTokenLabel(_pkcs11, info.TokenLabel);
                using var session = slot.OpenSession(SessionType.ReadWrite);
                session.Login(CKU.CKU_USER, _opt.UserPin);

                var ckaId = Convert.FromHexString(info.IdHex);
                var privKey = FindPrivateKeyById(session, ckaId)
                    ?? throw new DomainException("Private key not found in token for given ProviderRef.");

                session.DestroyObject(privKey);

                session.Logout();
            });
        }

        public Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef providerRef)
        {
            return Task.Run(() =>
            {
                var info = ParseProviderRef(providerRef);

                var slot = FindSlotByTokenLabel(_pkcs11, info.TokenLabel);
                using var session = slot.OpenSession(SessionType.ReadOnly);
                session.Login(CKU.CKU_USER, _opt.UserPin);

                var ckaId = Convert.FromHexString(info.IdHex);
                var pubKey = FindPublicKeyById(session, ckaId)
                    ?? throw new DomainException("Public key not found in token for given ProviderRef.");

                var pem = ExportRsaPublicKeyPem(session, pubKey);

                session.Logout();
                return new PublicKeyMaterial(pem);
            });
        }

        public Task<byte[]> SignDigestAsync(ProviderRef providerRef, Mechanism mechanism, byte[] digest)
        {
            return Task.Run(() =>
            {
                if (digest is null) throw new ArgumentNullException(nameof(digest));

                // MVP: RSA-PSS-SHA256 expects a 32-byte digest
                if (!string.Equals(mechanism.Name, "RSA_PSS_SHA256", StringComparison.OrdinalIgnoreCase))
                    throw new DomainException($"Unsupported mechanism '{mechanism.Name}'. MVP supports only RSA_PSS_SHA256.");

                if (digest.Length != 32)
                    throw new DomainException($"Invalid digest length. RSA_PSS_SHA256 requires 32 bytes, got {digest.Length}.");

                var info = ParseProviderRef(providerRef);

                var slot = FindSlotByTokenLabel(_pkcs11, info.TokenLabel);
                using var session = slot.OpenSession(SessionType.ReadOnly);
                session.Login(CKU.CKU_USER, _opt.UserPin);

                var ckaId = Convert.FromHexString(info.IdHex);
                var privKey = FindPrivateKeyById(session, ckaId)
                    ?? throw new DomainException("Private key not found in token for given ProviderRef.");

                // RSA-PSS params: hash SHA-256, MGF1 SHA-256, saltlen = 32
                var pssParams = new CkRsaPkcsPssParams(
                    (uint)CKM.CKM_SHA256,
                    (uint)CKG.CKG_MGF1_SHA256,
                    32
                );

                var signMech = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS_PSS, pssParams);

                var signature = session.Sign(signMech, privKey, digest);

                session.Logout();
                return signature;
            });
        }


        private static ISlot FindSlotByTokenLabel(IPkcs11Library pkcs11, string tokenLabel)
        {
            var slots = pkcs11.GetSlotList(SlotsType.WithTokenPresent);
            if (slots.Count == 0)
                throw new DomainException("No PKCS#11 token present.");

            foreach (var slot in pkcs11.GetSlotList(SlotsType.WithTokenPresent))
            {
                var ti = slot.GetTokenInfo();
                var label = (ti.Label ?? string.Empty).Trim();
                if (string.Equals(label, tokenLabel, StringComparison.Ordinal))
                    return slot;
            }

            throw new DomainException($"Token with label '{tokenLabel}' not found.");
        }

        private static IObjectHandle? FindPrivateKeyById(ISession session, byte[] ckaId)
        {
            var attrs = new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, ckaId),
        };

            session.FindObjectsInit(attrs);
            var found = session.FindObjects(1).FirstOrDefault();
            session.FindObjectsFinal();
            return found;
        }

        private static IObjectHandle? FindPublicKeyById(ISession session, byte[] ckaId)
        {
            var attrs = new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, ckaId),
        };

            session.FindObjectsInit(attrs);
            var found = session.FindObjects(1).FirstOrDefault();
            session.FindObjectsFinal();
            return found;
        }

        private static string ExportRsaPublicKeyPem(ISession session, IObjectHandle pubKey)
        {
            var values = session.GetAttributeValue(pubKey, new List<CKA> { CKA.CKA_MODULUS, CKA.CKA_PUBLIC_EXPONENT });

            var modulus = values[0].GetValueAsByteArray();
            var exponent = values[1].GetValueAsByteArray();

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });

            var spkiDer = rsa.ExportSubjectPublicKeyInfo();
            return ToPem("PUBLIC KEY", spkiDer);
        }

        private static string ToPem(string label, byte[] der)
        {
            var b64 = Convert.ToBase64String(der);
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {label}-----");
            for (int i = 0; i < b64.Length; i += 64)
                sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
            sb.AppendLine($"-----END {label}-----");
            return sb.ToString();
        }

        private sealed record ProviderRefInfo(string TokenLabel, string IdHex, string? Label);

        private static ProviderRefInfo ParseProviderRef(ProviderRef providerRef)
        {
            if (!string.Equals(providerRef.ProviderType, "PKCS11", StringComparison.OrdinalIgnoreCase))
                throw new DomainException($"ProviderRef.Provider must be 'PKCS11', got '{providerRef.ProviderType}'.");

            var parts = providerRef.Reference.Split(';', StringSplitOptions.RemoveEmptyEntries);

            string? token = null;
            string? id = null;
            string? label = null;

            foreach (var p in parts)
            {
                var kv = p.Split('=', 2);
                if (kv.Length != 2) continue;

                if (kv[0] == "token") token = kv[1];
                if (kv[0] == "id") id = kv[1];
                if (kv[0] == "label") label = kv[1];
            }

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(id))
                throw new DomainException("Invalid ProviderRef.Reference. Expected 'token=...;id=...;label=...'(label optional).");

            return new ProviderRefInfo(token, id, label);
        }
    }

}
