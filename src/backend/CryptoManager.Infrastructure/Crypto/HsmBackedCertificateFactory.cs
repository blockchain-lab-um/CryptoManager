using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class HsmBackedCertificateFactory
{
    public X509Certificate CreateSelfSignedCa(
        string publicKeyPem,
        X509Name subject,
        DateTime notBefore,
        DateTime notAfter,
        Org.BouncyCastle.Math.BigInteger serial,
        Func<byte[], Task<byte[]>> signDigestAsync)
    {
        var publicKey = ReadPublicKey(publicKeyPem);
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(serial);
        generator.SetIssuerDN(subject);
        generator.SetSubjectDN(subject);
        generator.SetNotBefore(notBefore);
        generator.SetNotAfter(notAfter);
        generator.SetPublicKey(publicKey);
        generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
        generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
        generator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(publicKey));
        generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifierStructure(publicKey));

        return generator.Generate(new HsmRsaPssSignatureFactory(signDigestAsync));
    }

    public X509Certificate CreateIssuedCertificate(
        Pkcs10CertificationRequest csr,
        X509Certificate issuerCertificate,
        DateTime notBefore,
        DateTime notAfter,
        Org.BouncyCastle.Math.BigInteger serial,
        Func<byte[], Task<byte[]>> signDigestAsync)
    {
        var subjectPublicKey = csr.GetPublicKey();
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(serial);
        generator.SetIssuerDN(issuerCertificate.SubjectDN);
        generator.SetSubjectDN(csr.GetCertificationRequestInfo().Subject);
        generator.SetNotBefore(notBefore);
        generator.SetNotAfter(notAfter);
        generator.SetPublicKey(subjectPublicKey);
        generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
        generator.AddExtension(
            X509Extensions.KeyUsage,
            true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation));
        generator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(subjectPublicKey));
        generator.AddExtension(
            X509Extensions.AuthorityKeyIdentifier,
            false,
            new AuthorityKeyIdentifierStructure(issuerCertificate));

        return generator.Generate(new HsmRsaPssSignatureFactory(signDigestAsync));
    }

    private static AsymmetricKeyParameter ReadPublicKey(string publicKeyPem)
    {
        var pemBase64 = string.Concat(
            publicKeyPem.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("-----", StringComparison.Ordinal))
                .Select(line => line.Trim()));

        return PublicKeyFactory.CreateKey(Convert.FromBase64String(pemBase64));
    }
}
