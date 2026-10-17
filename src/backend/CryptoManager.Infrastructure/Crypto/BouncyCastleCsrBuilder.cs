using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.ValueObjects;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Builds a DER-encoded PKCS#10 CSR. The subject key's private key never leaves the
/// HSM: signing is delegated to a <see cref="Func{T, Task}"/> callback.
/// </summary>
public sealed class BouncyCastleCsrBuilder : ICsrBuilder
{
    public async Task<byte[]> BuildCsrAsync(
        string publicKeyPem,
        SubjectDN subject,
        Func<byte[], Task<byte[]>> signDigestAsync,
        CancellationToken ct = default)
    {
        // Load the subject's public key from PEM.
        var subjectPublicKey = PublicKeyFactory.CreateKey(
            Convert.FromBase64String(ExtractBase64FromPem(publicKeyPem)));

        var subjectX509Name = new X509Name(subject.Value);

        // Build CertificationRequestInfo (TBS portion of the CSR).
        var csrInfo = new CertificationRequestInfo(
            subjectX509Name,
            SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(subjectPublicKey),
            new DerSet());

        var csrInfoDer = csrInfo.GetDerEncoded();

        // SHA-256 hash the TBS, then sign via HSM callback.
        byte[] digest;
        using (var sha = SHA256.Create())
            digest = sha.ComputeHash(csrInfoDer);

        var rawSignature = await signDigestAsync(digest).ConfigureAwait(false);

        // RSA-PSS SHA-256 algorithm identifier (mirrors HsmRsaPssSignatureFactory).
        var sigAlgId = BuildRsaPssSha256AlgorithmIdentifier();

        var csr = new CertificationRequest(csrInfo, sigAlgId, new DerBitString(rawSignature));
        return csr.GetDerEncoded();
    }

    private static AlgorithmIdentifier BuildRsaPssSha256AlgorithmIdentifier()
    {
        var hashAlg = new AlgorithmIdentifier(
            Org.BouncyCastle.Asn1.Nist.NistObjectIdentifiers.IdSha256,
            DerNull.Instance);
        var mgf1 = new AlgorithmIdentifier(
            Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.IdMgf1, hashAlg);
        var pss = new RsassaPssParameters(hashAlg, mgf1, new DerInteger(32), new DerInteger(1));
        return new AlgorithmIdentifier(
            Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers.IdRsassaPss, pss);
    }

    private static string ExtractBase64FromPem(string pem)
    {
        var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(
            lines.Where(l => !l.StartsWith("-----"))
                 .Select(l => l.Trim()));
    }
}