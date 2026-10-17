using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class BouncyCastlePkcs7AttachedSigner : IPkcs7AttachedSigner
{
    public async Task<(string OutputFileName, byte[] Bytes)> SignAttachedAsync(
        string originalFileName,
        byte[] fileBytes,
        DocumentSigningMaterial material,
        CancellationToken ct)
    {
        if (material.Mechanism == Mechanism.EcdsaP256Sha256Der)
            throw new NotSupportedException("ECDSA document signing is not yet supported. Use RSA_PSS_SHA256.");

        var parser = new X509CertificateParser();
        var leafCert = parser.ReadCertificate(material.CertBundle.LeafDer);

        var allCerts = new List<X509Certificate> { leafCert };
        foreach (var chainCertDer in material.CertBundle.ChainDer)
            allCerts.Add(parser.ReadCertificate(chainCertDer));

        var certStore = new SimpleX509Store(allCerts);

        var signatureFactory = new HsmRsaPssSignatureFactory(material.SignDigestAsync);

        var signerInfoGen = new SignerInfoGeneratorBuilder()
            .Build(signatureFactory, leafCert);

        var gen = new CmsSignedDataGenerator();
        gen.AddSignerInfoGenerator(signerInfoGen);
        gen.AddCertificates(certStore);

        var msg = new CmsProcessableByteArray(fileBytes);
        var signedData = gen.Generate(msg, encapsulate: true);
        var p7MBytes = signedData.GetEncoded();

        var outputFileName = originalFileName + ".p7m";
        return await Task.FromResult((outputFileName, p7MBytes));
    }

    /// <summary>
    /// ISignatureFactory for RSA-PSS-SHA256, delegating the actual signing to an HSM.
    /// BouncyCastle 2.x uses ISignatureFactory/IStreamCalculator instead of the old IContentSigner.
    /// </summary>
    private sealed class HsmRsaPssSignatureFactory : ISignatureFactory
    {
        private readonly Func<byte[], Task<byte[]>> _signDigest;

        public HsmRsaPssSignatureFactory(Func<byte[], Task<byte[]>> signDigest)
        {
            _signDigest = signDigest;
            AlgorithmDetails = BuildAlgorithmIdentifier();
        }

        public object AlgorithmDetails { get; }

        public IStreamCalculator<IBlockResult> CreateCalculator() =>
            new HsmStreamCalculator(_signDigest);

        private static AlgorithmIdentifier BuildAlgorithmIdentifier()
        {
            var hashAlg = new AlgorithmIdentifier(NistObjectIdentifiers.IdSha256, DerNull.Instance);
            var mgf1 = new AlgorithmIdentifier(PkcsObjectIdentifiers.IdMgf1, hashAlg);
            var pss = new RsassaPssParameters(hashAlg, mgf1, new DerInteger(32), new DerInteger(1));
            return new AlgorithmIdentifier(PkcsObjectIdentifiers.IdRsassaPss, pss);
        }
    }

    private sealed class HsmStreamCalculator : IStreamCalculator<IBlockResult>
    {
        private readonly Func<byte[], Task<byte[]>> _signDigest;
        private readonly MemoryStream _buffer = new();

        public HsmStreamCalculator(Func<byte[], Task<byte[]>> signDigest)
        {
            _signDigest = signDigest;
        }

        public Stream Stream => _buffer;

        public IBlockResult GetResult()
        {
            var data = _buffer.ToArray();
            byte[] digest;
            using (var sha = SHA256.Create())
                digest = sha.ComputeHash(data);
            var sig = _signDigest(digest).GetAwaiter().GetResult();
            return new ByteArrayBlockResult(sig);
        }
    }

    private sealed class ByteArrayBlockResult : IBlockResult
    {
        private readonly byte[] _bytes;

        public ByteArrayBlockResult(byte[] bytes) => _bytes = bytes;

        public byte[] Collect() => _bytes;

        public int Collect(byte[] destination, int offset)
        {
            _bytes.CopyTo(destination, offset);
            return _bytes.Length;
        }

        public int Collect(Span<byte> destination)
        {
            _bytes.CopyTo(destination);
            return _bytes.Length;
        }

        public int GetMaxResultLength() => _bytes.Length;
    }

    /// <summary>
    /// Minimal IStore&lt;X509Certificate&gt; that holds a set of certificates for CMS inclusion.
    /// </summary>
    private sealed class SimpleX509Store : IStore<X509Certificate>
    {
        private readonly List<X509Certificate> _certs;

        public SimpleX509Store(List<X509Certificate> certs) => _certs = certs;

        public IEnumerable<X509Certificate> EnumerateMatches(ISelector<X509Certificate>? selector) =>
            selector == null ? _certs : _certs.Where(c => selector.Match(c));
    }
}