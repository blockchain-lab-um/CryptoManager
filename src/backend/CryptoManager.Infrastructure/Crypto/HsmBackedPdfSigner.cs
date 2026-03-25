using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using PdfSharp.Pdf.Signatures;
using System.Security.Cryptography;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// PDFsharp IDigitalSigner implementation that delegates signing to an HSM callback.
/// The private key never leaves the HSM; only the digest is passed out.
/// </summary>
internal sealed class HsmBackedPdfSigner : IDigitalSigner
{
    private readonly byte[] _leafDer;
    private readonly byte[][] _chainDer;
    private readonly Func<byte[], Task<byte[]>> _signDigestAsync;

    public HsmBackedPdfSigner(
        byte[] leafDer,
        byte[][] chainDer,
        Func<byte[], Task<byte[]>> signDigestAsync)
    {
        _leafDer = leafDer;
        _chainDer = chainDer;
        _signDigestAsync = signDigestAsync;
    }

    /// <summary>
    /// Returns the subject name from the leaf certificate, used by PDFsharp for the signature field.
    /// </summary>
    public string CertificateName
    {
        get
        {
            var cert = new X509CertificateParser().ReadCertificate(_leafDer);
            return cert.SubjectDN.ToString();
        }
    }

    /// <summary>
    /// Estimates the CMS signature size so PDFsharp can reserve the right amount of space.
    /// RSA-2048 PSS + one CA cert chain: ~8 KB is a safe upper bound.
    /// </summary>
    public Task<int> GetSignatureSizeAsync() => Task.FromResult(8192);

    /// <summary>
    /// Called by PDFsharp with the bytes to sign. Returns a CMS SignedData blob.
    /// </summary>
    public Task<byte[]> GetSignatureAsync(Stream rangeStream)
    {
        var data = ReadAllBytes(rangeStream);

        byte[] digest;
        using (var sha = SHA256.Create())
            digest = sha.ComputeHash(data);

        var rawSig = _signDigestAsync(digest).GetAwaiter().GetResult();

        return Task.FromResult(BuildCmsSignedData(data, rawSig));
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                break;

            ms.Write(buffer, 0, bytesRead);
        }

        return ms.ToArray();
    }

    private byte[] BuildCmsSignedData(byte[] data, byte[] rawSignature)
    {
        var parser = new X509CertificateParser();
        var leafCert = parser.ReadCertificate(_leafDer);

        var allCerts = new List<X509Certificate> { leafCert };
        foreach (var chainCertDer in _chainDer)
            allCerts.Add(parser.ReadCertificate(chainCertDer));

        var certStore = new SimpleX509Store(allCerts);

        var hashAlg = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
            NistObjectIdentifiers.IdSha256, DerNull.Instance);
        var mgf1 = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
            PkcsObjectIdentifiers.IdMgf1, hashAlg);
        var pss = new RsassaPssParameters(hashAlg, mgf1, new DerInteger(32), new DerInteger(1));
        var sigAlgId = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
            PkcsObjectIdentifiers.IdRsassaPss, pss);

        var signerInfoGen = new SignerInfoGeneratorBuilder()
            .Build(new PrecomputedSignatureFactory(sigAlgId, rawSignature), leafCert);

        var gen = new CmsSignedDataGenerator();
        gen.AddSignerInfoGenerator(signerInfoGen);
        gen.AddCertificates(certStore);

        var msg = new CmsProcessableByteArray(data);
        var signedData = gen.Generate(msg, encapsulate: false);
        return signedData.GetEncoded();
    }

    private sealed class SimpleX509Store : IStore<X509Certificate>
    {
        private readonly List<X509Certificate> _certs;
        public SimpleX509Store(List<X509Certificate> certs) => _certs = certs;

        public IEnumerable<X509Certificate> EnumerateMatches(ISelector<X509Certificate>? selector) =>
            selector == null ? _certs : _certs.Where(c => selector.Match(c));
    }

    /// <summary>
    /// ISignatureFactory that returns a pre-computed signature (from HSM callback).
    /// </summary>
    private sealed class PrecomputedSignatureFactory : ISignatureFactory
    {
        private readonly byte[] _signature;

        public PrecomputedSignatureFactory(object algorithmDetails, byte[] signature)
        {
            AlgorithmDetails = algorithmDetails;
            _signature = signature;
        }

        public object AlgorithmDetails { get; }

        public IStreamCalculator<IBlockResult> CreateCalculator() =>
            new NullStreamCalculator(_signature);
    }

    private sealed class NullStreamCalculator : IStreamCalculator<IBlockResult>
    {
        private readonly byte[] _signature;

        public NullStreamCalculator(byte[] signature)
        {
            _signature = signature;
            Stream = Stream.Null;
        }

        public Stream Stream { get; }

        public IBlockResult GetResult() => new ByteArrayBlockResult(_signature);
    }

    private sealed class ByteArrayBlockResult : IBlockResult
    {
        private readonly byte[] _bytes;
        public ByteArrayBlockResult(byte[] bytes) => _bytes = bytes;
        public byte[] Collect() => _bytes;
        public int Collect(byte[] destination, int offset) { _bytes.CopyTo(destination, offset); return _bytes.Length; }
        public int Collect(Span<byte> destination) { _bytes.CopyTo(destination); return _bytes.Length; }
        public int GetMaxResultLength() => _bytes.Length;
    }
}
