using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoManager.Infrastructure.Crypto;

public sealed class SignedFileVerifier : ISignedFileVerifier
{
    private static readonly Regex ByteRangeRegex = new(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.Compiled);
    public Task<VerifySignedFileResult> VerifyAsync(VerifySignedFileCommand command, CancellationToken ct = default)
    {
        var bytes = command.FileBytes;
        if (IsPdf(bytes))
            return Task.FromResult(VerifyPdf(command.FileName, bytes));

        return Task.FromResult(VerifyCmsContainer(bytes));
    }

    private static VerifySignedFileResult VerifyPdf(string fileName, byte[] pdfBytes)
    {
        var text = Encoding.ASCII.GetString(pdfBytes);
        var byteRangeMatch = ByteRangeRegex.Match(text);

        if (!byteRangeMatch.Success)
        {
            return new VerifySignedFileResult(
                IsValid: false,
                Format: "PDF",
                Message: $"'{fileName}' does not contain an embedded PDF signature.");
        }

        if (!TryParseByteRange(byteRangeMatch, pdfBytes.Length, out var rangeStart0, out var rangeLength0, out var rangeStart1, out var rangeLength1, out var error))
        {
            return new VerifySignedFileResult(false, "PAdES", error!);
        }

        byte[] signedBytes = new byte[rangeLength0 + rangeLength1];
        Buffer.BlockCopy(pdfBytes, rangeStart0, signedBytes, 0, rangeLength0);
        Buffer.BlockCopy(pdfBytes, rangeStart1, signedBytes, rangeLength0, rangeLength1);

        Exception? lastError = null;
        foreach (var cmsBytes in ExtractPdfSignatureContentsCandidates(pdfBytes, rangeStart0, rangeLength0, rangeStart1))
        {
            try
            {
                var cms = new CmsSignedData(new CmsProcessableByteArray(signedBytes), cmsBytes);
                return VerifyCmsSignedData(cms, "PAdES");
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        return new VerifySignedFileResult(
            false,
            "PAdES",
            $"Embedded CMS signature could not be verified: {lastError?.Message ?? "Unknown CMS parsing error."}");
    }

    private static VerifySignedFileResult VerifyCmsContainer(byte[] fileBytes)
    {
        try
        {
            var cms = new CmsSignedData(fileBytes);
            return VerifyCmsSignedData(cms, "PKCS7-Attached");
        }
        catch (Exception ex)
        {
            return new VerifySignedFileResult(
                IsValid: false,
                Format: "Unknown",
                Message: $"Unsupported signed-file format or malformed container: {ex.Message}");
        }
    }

    private static VerifySignedFileResult VerifyCmsSignedData(CmsSignedData cms, string format)
    {
        var signers = cms.GetSignerInfos().GetSigners().Cast<SignerInformation>().ToList();
        if (signers.Count == 0)
            return new VerifySignedFileResult(false, format, "No signer information found in the signed file.");

        var certStore = cms.GetCertificates();
        foreach (var signer in signers)
        {
            var cert = certStore.EnumerateMatches(signer.SignerID).OfType<X509Certificate>().FirstOrDefault();
            if (cert is null)
                return new VerifySignedFileResult(false, format, "No signing certificate was found for the embedded signer.");

            if (!signer.Verify(cert))
                return new VerifySignedFileResult(false, format, "Cryptographic signature verification failed.");

            return new VerifySignedFileResult(
                IsValid: true,
                Format: format,
                Message: "Cryptographic signature is valid. Certificate trust was not evaluated.",
                SignerName: GetCertificateCommonName(cert),
                CertificateSubject: cert.SubjectDN.ToString(),
                SigningTime: GetSigningTime(signer));
        }

        return new VerifySignedFileResult(false, format, "No signer could be verified.");
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length >= 5
        && bytes[0] == 0x25
        && bytes[1] == 0x50
        && bytes[2] == 0x44
        && bytes[3] == 0x46
        && bytes[4] == 0x2D;

    private static bool TryParseByteRange(
        Match match,
        int fileLength,
        out int start0,
        out int length0,
        out int start1,
        out int length1,
        out string? error)
    {
        start0 = length0 = start1 = length1 = 0;
        error = null;

        if (!int.TryParse(match.Groups[1].Value, out start0)
            || !int.TryParse(match.Groups[2].Value, out length0)
            || !int.TryParse(match.Groups[3].Value, out start1)
            || !int.TryParse(match.Groups[4].Value, out length1))
        {
            error = "Embedded PDF signature has an invalid ByteRange.";
            return false;
        }

        if (start0 != 0 || length0 < 0 || start1 < length0 || length1 < 0 || start1 + length1 > fileLength)
        {
            error = "Embedded PDF signature has an out-of-range ByteRange.";
            return false;
        }

        return true;
    }

    private static IEnumerable<byte[]> ExtractPdfSignatureContentsCandidates(byte[] pdfBytes, int rangeStart0, int rangeLength0, int rangeStart1)
    {
        var gapStart = rangeStart0 + rangeLength0;
        var gapLength = rangeStart1 - gapStart;
        if (gapLength <= 0)
            throw new InvalidOperationException("Embedded PDF signature has an empty ByteRange gap.");

        var gapText = Encoding.ASCII.GetString(pdfBytes, gapStart, gapLength);
        var open = gapText.IndexOf('<');
        var close = gapText.LastIndexOf('>');
        if (open < 0 || close <= open)
            throw new InvalidOperationException("Embedded PDF signature contents could not be located in the ByteRange gap.");

        var hex = gapText[(open + 1)..close];
        var cleaned = Regex.Replace(hex, @"\s+", string.Empty);
        if (cleaned.Length == 0)
            throw new InvalidOperationException("Embedded PDF signature contents are empty.");

        var placeholderBytes = Convert.FromHexString(cleaned);
        var startOffset = FindCmsObjectStart(placeholderBytes);
        var bytes = startOffset >= 0 ? placeholderBytes[startOffset..] : placeholderBytes;
        var yieldedLengths = new HashSet<int>();

        if (TryGetDerObjectLength(bytes, out var cmsLength) && yieldedLengths.Add(cmsLength))
            yield return bytes[..cmsLength];

        if (bytes.Length >= 2 && bytes[0] == 0x30 && bytes[1] == 0x80)
        {
            var trailingZeroCount = 0;
            for (var i = bytes.Length - 1; i >= 0 && bytes[i] == 0x00; i--)
                trailingZeroCount++;

            if (trailingZeroCount >= 2)
            {
                var contentEnd = bytes.Length - trailingZeroCount;
                var maxCandidateZeroBytes = Math.Min(trailingZeroCount, 32);
                for (var zeroBytes = 2; zeroBytes <= maxCandidateZeroBytes; zeroBytes += 2)
                {
                    var candidateLength = contentEnd + zeroBytes;
                    if (candidateLength > 0 && candidateLength <= bytes.Length && yieldedLengths.Add(candidateLength))
                        yield return bytes[..candidateLength];
                }
            }
        }

        var trimmedLength = bytes.Length;
        while (trimmedLength > 0 && bytes[trimmedLength - 1] == 0x00)
            trimmedLength--;

        if (trimmedLength > 0 && yieldedLengths.Add(trimmedLength))
            yield return bytes[..trimmedLength];

        if (yieldedLengths.Add(bytes.Length))
            yield return bytes;
    }

    private static int FindCmsObjectStart(byte[] bytes)
    {
        for (int i = 0; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == 0x30)
                return i;
        }

        return -1;
    }

    private static bool TryGetDerObjectLength(ReadOnlySpan<byte> bytes, out int totalLength)
    {
        totalLength = 0;
        if (bytes.Length < 2)
            return false;

        int lengthByte = bytes[1];
        if ((lengthByte & 0x80) == 0)
        {
            totalLength = 2 + lengthByte;
            return totalLength <= bytes.Length;
        }

        int lengthOctetCount = lengthByte & 0x7F;
        if (lengthOctetCount <= 0 || lengthOctetCount > 4 || bytes.Length < 2 + lengthOctetCount)
            return false;

        int contentLength = 0;
        for (int i = 0; i < lengthOctetCount; i++)
        {
            contentLength = (contentLength << 8) | bytes[2 + i];
        }

        totalLength = 2 + lengthOctetCount + contentLength;
        return totalLength > 0 && totalLength <= bytes.Length;
    }

    private static string GetCertificateCommonName(X509Certificate certificate)
    {
        var subject = certificate.SubjectDN.ToString();
        if (string.IsNullOrWhiteSpace(subject))
            return "Unknown signer";

        var match = Regex.Match(subject, @"(?:^|,\s*)CN=(?<value>[^,]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : subject;
    }

    private static DateTimeOffset? GetSigningTime(SignerInformation signer)
    {
        try
        {
            var attrs = signer.SignedAttributes;
            var attr = attrs?[PkcsObjectIdentifiers.Pkcs9AtSigningTime];
            if (attr is null || attr.AttrValues.Count == 0)
                return null;

            var value = attr.AttrValues[0].ToAsn1Object();

            if (value is DerUtcTime utcTime)
                return new DateTimeOffset(DateTime.SpecifyKind(utcTime.ToAdjustedDateTime(), DateTimeKind.Utc));

            if (value is DerGeneralizedTime generalizedTime)
                return new DateTimeOffset(DateTime.SpecifyKind(generalizedTime.ToDateTime(), DateTimeKind.Utc));

            return null;
        }
        catch
        {
            return null;
        }
    }
}
