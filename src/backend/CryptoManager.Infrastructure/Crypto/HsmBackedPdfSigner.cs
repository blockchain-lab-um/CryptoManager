using System.Reflection;
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
using Microsoft.Extensions.Logging;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// PDFsharp IDigitalSigner implementation that delegates signing to an HSM callback.
/// The private key never leaves the HSM; only the digest is passed out.
/// </summary>
public sealed class HsmBackedPdfSigner : IDigitalSigner
{
    private readonly byte[] _leafDer;
    private readonly byte[][] _chainDer;
    private readonly Func<byte[], Task<byte[]>> _signDigestAsync;
    private readonly ILogger<HsmBackedPdfSigner> _logger;

    public HsmBackedPdfSigner(
        byte[] leafDer,
        byte[][] chainDer,
        Func<byte[], Task<byte[]>> signDigestAsync,
        ILogger<HsmBackedPdfSigner> logger)
    {
        _leafDer = leafDer;
        _chainDer = chainDer;
        _signDigestAsync = signDigestAsync;
        _logger = logger;
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
        var data = TryReadPdfSharpRangedStream(rangeStream) ?? ReadAllBytes(rangeStream);

        if (data.Length == 0)
            throw new InvalidOperationException(
                "PDF signature input was empty. Refusing to produce an invalid signature.");
        
        string hex = BitConverter.ToString(data);
        
        
        byte[] digest;
        using (var sha = SHA256.Create())
            digest = sha.ComputeHash(data);

        _logger.LogInformation(
            "PAdES signing input: rangeLength={RangeLength}, rangeSha256={RangeSha256}",
            data.Length,
            Convert.ToHexStringLower(digest));

        var cms = BuildCmsSignedData(data);
        var cmsDumpPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"cryptomanager-pades-{Guid.NewGuid():N}.p7s");
        System.IO.File.WriteAllBytes(cmsDumpPath, cms);
        _logger.LogInformation("PAdES CMS dump written to {CmsDumpPath}", cmsDumpPath);
        _logger.LogInformation("PAdES CMS packaging: cmsLength={CmsLength}", cms.Length);

        return Task.FromResult(cms);
    }

    private byte[]? TryReadPdfSharpRangedStream(Stream stream)
    {
        var type = stream.GetType();
        if (type.FullName != "PdfSharp.Pdf.Signatures.RangedStream")
            return null;

        var rangesField = type.GetField("_ranges", BindingFlags.Instance | BindingFlags.NonPublic);
        var backingField = type.GetField("<Stream>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (rangesField?.GetValue(stream) is not Array ranges || backingField?.GetValue(stream) is not Stream backingStream)
            return null;

        var originalPosition = backingStream.CanSeek ? backingStream.Position : 0;
        try
        {
            using var ms = new MemoryStream();

            foreach (var range in ranges)
            {
                if (range is null)
                    continue;

                var rangeType = range.GetType();
                var offsetField = rangeType.GetField("<Offset>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                var lengthField = rangeType.GetField("<Length>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (offsetField?.GetValue(range) is not long offset || lengthField?.GetValue(range) is not long length)
                    throw new InvalidOperationException("Unable to read PDFsharp range metadata.");

                CopyRange(backingStream, ms, offset, length);
            }

            return ms.ToArray();
        }
        finally
        {
            if (backingStream.CanSeek)
            {
                backingStream.Position = originalPosition;
            }
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var bytesRead = stream.Read(buffer.AsSpan(0, buffer.Length));
            if (bytesRead == 0)
                break;

            ms.Write(buffer, 0, bytesRead);
        }

        return ms.ToArray();
    }

    private static void CopyRange(Stream source, Stream destination, long offset, long length)
    {
        source.Position = offset;
        var remaining = length;
        var buffer = new byte[81920];

        while (remaining > 0)
        {
            var chunkSize = (int)Math.Min(buffer.Length, remaining);
            var bytesRead = source.Read(buffer, 0, chunkSize);
            if (bytesRead <= 0)
                throw new EndOfStreamException($"Unexpected end of backing PDF stream while reading range offset={offset}, length={length}.");

            destination.Write(buffer, 0, bytesRead);
            remaining -= bytesRead;
        }
    }

    private byte[] BuildCmsSignedData(byte[] data)
    {
        var parser = new X509CertificateParser();
        var leafCert = parser.ReadCertificate(_leafDer);

        var allCerts = new List<X509Certificate> { leafCert };
        foreach (var chainCertDer in _chainDer)
            allCerts.Add(parser.ReadCertificate(chainCertDer));

        var certStore = new SimpleX509Store(allCerts);

        var signerInfoGen = new SignerInfoGeneratorBuilder()
            .Build(new HsmRsaPssSignatureFactory(_signDigestAsync, _logger), leafCert);

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
    /// ISignatureFactory for RSA-PSS-SHA256, delegating the actual signing to an HSM.
    /// BouncyCastle streams the DER-encoded signed attributes into the calculator when attributes are present.
    /// </summary>
    private sealed class HsmRsaPssSignatureFactory : ISignatureFactory
    {
        private readonly Func<byte[], Task<byte[]>> _signDigest;
        private readonly ILogger<HsmBackedPdfSigner> _logger;

        public HsmRsaPssSignatureFactory(
            Func<byte[], Task<byte[]>> signDigest,
            ILogger<HsmBackedPdfSigner> logger)
        {
            _signDigest = signDigest;
            _logger = logger;
            AlgorithmDetails = BuildAlgorithmIdentifier();
        }

        public object AlgorithmDetails { get; }

        public IStreamCalculator<IBlockResult> CreateCalculator() =>
            new HsmStreamCalculator(_signDigest, _logger);

        private static Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier BuildAlgorithmIdentifier()
        {
            var hashAlg = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
                NistObjectIdentifiers.IdSha256, DerNull.Instance);
            var mgf1 = new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
                PkcsObjectIdentifiers.IdMgf1, hashAlg);
            var pss = new RsassaPssParameters(hashAlg, mgf1, new DerInteger(32), new DerInteger(1));
            return new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(
                PkcsObjectIdentifiers.IdRsassaPss, pss);
        }
    }

    private sealed class HsmStreamCalculator : IStreamCalculator<IBlockResult>
    {
        private readonly Func<byte[], Task<byte[]>> _signDigest;
        private readonly ILogger<HsmBackedPdfSigner> _logger;
        private readonly MemoryStream _buffer = new();

        public HsmStreamCalculator(
            Func<byte[], Task<byte[]>> signDigest,
            ILogger<HsmBackedPdfSigner> logger)
        {
            _signDigest = signDigest;
            _logger = logger;
        }

        public Stream Stream => _buffer;

        public IBlockResult GetResult()
        {
            var data = _buffer.ToArray();
            byte[] digest;
            using (var sha = SHA256.Create())
                digest = sha.ComputeHash(data);

            _logger.LogInformation(
                "PAdES CMS signature input: signedAttributesLength={SignedAttributesLength}, signedAttributesSha256={SignedAttributesSha256}",
                data.Length,
                Convert.ToHexStringLower(digest));

            var signature = _signDigest(digest).GetAwaiter().GetResult();

            _logger.LogInformation("PAdES signing output: rawSignatureLength={RawSignatureLength}", signature.Length);

            return new ByteArrayBlockResult(signature);
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
}
