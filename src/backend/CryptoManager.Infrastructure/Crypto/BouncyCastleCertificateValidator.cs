using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class BouncyCastleCertificateValidator : ICertificateValidator
{
    /// <summary>
    /// Returns true if the public key embedded in the DER certificate matches the
    /// public key exported in PEM format (e.g. from the HSM).
    /// </summary>
    public bool VerifyMatchesPublicKey(byte[] certDer, string publicKeyPem)
    {
        var cert = ParseCert(certDer);
        var certPublicKey = cert.GetPublicKey();

        AsymmetricKeyParameter pemPublicKey;
        try
        {
            var pemBase64 = ExtractBase64FromPem(publicKeyPem);
            pemPublicKey = PublicKeyFactory.CreateKey(Convert.FromBase64String(pemBase64));
        }
        catch
        {
            return false;
        }

        // Compare SubjectPublicKeyInfo DER bytes — algorithm-agnostic.
        var certSpki = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(certPublicKey).GetDerEncoded();
        var pemSpki  = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pemPublicKey).GetDerEncoded();

        return certSpki.SequenceEqual(pemSpki);
    }

    /// <summary>
    /// Validates the certificate chain at <paramref name="at"/>.
    /// <paramref name="chainDer"/> should contain the intermediates in order (closest to leaf first).
    /// For self-signed end-entity certs with an explicit root provided, include the root.
    /// </summary>
    public ChainValidationResult ValidateChain(byte[] certDer, byte[][] chainDer, DateTimeOffset at)
    {
        try
        {
            var leaf = ParseCert(certDer);
            var chain = (chainDer ?? []).Select(ParseCert).ToList();

            // Simple linear chain walk: each cert must be signed by the next.
            // For self-signed end-entity (no chain), nothing more to validate.
            if (chain.Count == 0)
            {
                // Accept: no chain provided; caller trusts the cert directly.
                CheckValidity(leaf, at);
                return ChainValidationResult.Ok();
            }

            CheckValidity(leaf, at);

            X509Certificate issuerCert = chain[0];
            leaf.Verify(issuerCert.GetPublicKey());

            for (int i = 0; i < chain.Count - 1; i++)
            {
                var subject = chain[i];
                var issuer  = chain[i + 1];
                CheckValidity(subject, at);
                subject.Verify(issuer.GetPublicKey());
            }

            // Root (last in chain) must be self-signed.
            var root = chain[^1];
            CheckValidity(root, at);
            root.Verify(root.GetPublicKey());

            return ChainValidationResult.Ok();
        }
        catch (Exception ex)
        {
            return ChainValidationResult.Fail(ex.Message);
        }
    }

    private static void CheckValidity(X509Certificate cert, DateTimeOffset at)
    {
        try { cert.CheckValidity(at.UtcDateTime); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Certificate '{cert.SubjectDN}' is not valid at {at:O}: {ex.Message}", ex);
        }
    }

    private static X509Certificate ParseCert(byte[] der)
        => new X509CertificateParser().ReadCertificate(der);

    private static string ExtractBase64FromPem(string pem)
    {
        var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(
            lines.Where(l => !l.StartsWith("-----"))
                 .Select(l => l.Trim()));
    }
}