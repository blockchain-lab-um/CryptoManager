using CryptoManager.Application.Abstractions;
using CryptoManager.Domain.ValueObjects;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class BouncyCastlePkcs7AttachedSigner : IPkcs7AttachedSigner
{
    private readonly IHsmProvider _hsm;

    public BouncyCastlePkcs7AttachedSigner(IHsmProvider hsm)
    {
        _hsm = hsm;
    }

    public async Task<(string OutputFileName, byte[] Bytes)> SignAttachedAsync(
        ProviderRef providerRef,
        Mechanism mechanism,
        string originalFileName,
        byte[] fileBytes,
        CancellationToken ct)
    {
        var cert = await _hsm.GetSigningCertificateAsync(providerRef);
        var bcCert = DotNetUtilities.FromX509Certificate(cert);

        var sigFactory = new HsmRsaPssSignatureFactory(
            digest => _hsm.SignDigestAsync(providerRef, mechanism, digest));

        var generator = new CmsSignedDataGenerator();
        generator.AddSignerInfoGenerator(
            new SignerInfoGeneratorBuilder().Build(sigFactory, bcCert));
        generator.AddCertificates(new SimpleX509Store(bcCert));

        var cmsData = generator.Generate(new CmsProcessableByteArray(fileBytes), encapsulate: true);

        return (originalFileName + ".p7m", cmsData.GetEncoded());
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
    /// Minimal IStore&lt;X509Certificate&gt; that holds a single certificate for CMS inclusion.
    /// </summary>
    private sealed class SimpleX509Store : IStore<X509Certificate>
    {
        private readonly X509Certificate[] _certs;

        public SimpleX509Store(X509Certificate cert) => _certs = [cert];

        public IEnumerable<X509Certificate> EnumerateMatches(ISelector<X509Certificate>? selector) =>
            selector == null ? _certs : _certs.Where(c => selector.Match(c));
    }
}
