using CryptoManager.Domain.ValueObjects;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;

namespace CryptoManager.Infrastructure.Crypto;

internal sealed class HsmRsaPssSignatureFactory : ISignatureFactory
{
    private readonly Func<byte[], Task<byte[]>> _signDigest;

    public HsmRsaPssSignatureFactory(Func<byte[], Task<byte[]>> signDigest)
    {
        _signDigest = signDigest;
        AlgorithmDetails = BuildAlgorithmIdentifier();
    }

    public object AlgorithmDetails { get; }

    public IStreamCalculator<IBlockResult> CreateCalculator() => new HsmStreamCalculator(_signDigest);

    public static AlgorithmIdentifier BuildAlgorithmIdentifier()
    {
        var hashAlg = new AlgorithmIdentifier(NistObjectIdentifiers.IdSha256, DerNull.Instance);
        var mgf1 = new AlgorithmIdentifier(PkcsObjectIdentifiers.IdMgf1, hashAlg);
        var pss = new RsassaPssParameters(hashAlg, mgf1, new DerInteger(32), new DerInteger(1));
        return new AlgorithmIdentifier(PkcsObjectIdentifiers.IdRsassaPss, pss);
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
            var signature = _signDigest(digest).GetAwaiter().GetResult();
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
