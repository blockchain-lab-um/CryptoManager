using CryptoManager.Application.Abstractions;
using CryptoManager.Application.DTOs;
using CryptoManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Signatures;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace CryptoManager.Infrastructure.Crypto;

/// <summary>
/// Signs PDF files using PAdES via PDFsharp 6.x, delegating the actual signing
/// operation to an HSM callback so private keys never leave the HSM.
/// </summary>
public sealed class PadesSigner : IPadesSigner
{
    private readonly ILogger<HsmBackedPdfSigner> _pdfSignerLogger;

    public PadesSigner(ILogger<HsmBackedPdfSigner> pdfSignerLogger)
    {
        _pdfSignerLogger = pdfSignerLogger;
    }

    public async Task<(string OutputFileName, byte[] Bytes)> SignPdfAsync(
        string originalFileName,
        byte[] pdfBytes,
        DocumentSigningMaterial material,
        CancellationToken ct)
    {
        if (material.Mechanism == Mechanism.EcdsaP256Sha256Der)
            throw new NotSupportedException("ECDSA document signing is not yet supported. Use RSA_PSS_SHA256.");

        var signer = new HsmBackedPdfSigner(
            material.CertBundle.LeafDer,
            material.CertBundle.ChainDer,
            material.SignDigestAsync,
            _pdfSignerLogger);

        byte[] signed;
        using (var inputStream = new MemoryStream(pdfBytes))
        using (var outputStream = new MemoryStream())
        {
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var originalModDate = document.Info.ModificationDate;

            AddVisibleSignatureStamp(document, material);

            DigitalSignatureHandler.ForDocument(document, signer, new DigitalSignatureOptions());
            
            document.Info.ModificationDate = originalModDate;
            document.Save(outputStream, closeStream: false);
            signed = outputStream.ToArray();
        }

        var outputFileName = Path.GetFileNameWithoutExtension(originalFileName) + "_signed.pdf";
        return await Task.FromResult((outputFileName, signed));
    }

    private static void AddVisibleSignatureStamp(PdfDocument document, DocumentSigningMaterial material)
    {
        if (document.PageCount == 0)
            return;

        var page = document.Pages[0];
        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

        const double boxWidth = 232;
        const double boxHeight = 108;
        const double margin = 24;
        const double radius = 10;

        var panelBrush = new XSolidBrush(XColor.FromArgb(248, 248, 248));
        var borderPen = new XPen(XColor.FromArgb(44, 44, 44), 1.2);
        var accentBrush = new XSolidBrush(XColor.FromArgb(28, 28, 28));
        var mutedBrush = new XSolidBrush(XColor.FromArgb(96, 96, 96));
        var linePen = new XPen(XColor.FromArgb(214, 214, 214), 1);
        var badgeBrush = new XSolidBrush(XColor.FromArgb(32, 32, 32));
        var badgeTextBrush = XBrushes.White;
        var iconPen = new XPen(XColor.FromArgb(255, 255, 255), 2.2)
        {
            LineCap = XLineCap.Round
        };

        var titleFont = new XFont("Switzer", 10, XFontStyleEx.Bold);
        var bodyFont = new XFont("Switzer", 8.5, XFontStyleEx.Regular);
        var labelFont = new XFont("Switzer", 7.5, XFontStyleEx.Bold);

        ApplyPageOrientationTransform(gfx, page);

        var orientedWidth = GetOrientedPageWidth(page);
        var orientedHeight = GetOrientedPageHeight(page);
        var rect = new XRect(
            orientedWidth - boxWidth - margin,
            orientedHeight - boxHeight - margin,
            boxWidth,
            boxHeight);

        gfx.DrawRoundedRectangle(borderPen, panelBrush, rect, new XSize(radius, radius));

        var badgeRect = new XRect(rect.X + 12, rect.Y + 10, 104, 18);
        gfx.DrawRoundedRectangle(XPens.Transparent, badgeBrush, badgeRect, new XSize(9, 9));
        gfx.DrawString("DIGITALLY SIGNED", labelFont, badgeTextBrush, badgeRect, XStringFormats.Center);

        var iconCenterX = rect.Right - 22;
        var iconCenterY = rect.Y + 19;
        var iconRect = new XRect(iconCenterX - 10, iconCenterY - 10, 20, 20);
        gfx.DrawEllipse(XPens.Transparent, accentBrush, iconRect);
        gfx.DrawLine(iconPen, iconCenterX - 4, iconCenterY + 1, iconCenterX - 1, iconCenterY + 4);
        gfx.DrawLine(iconPen, iconCenterX - 1, iconCenterY + 4, iconCenterX + 5, iconCenterY - 3);

        gfx.DrawLine(linePen, rect.X + 12, rect.Y + 34, rect.Right - 12, rect.Y + 34);

        var requestedBy = string.IsNullOrWhiteSpace(material.RequestedBy) ? "Unknown user" : material.RequestedBy;
        var certificateName = GetCertificateDisplayName(material.CertBundle.LeafDer);
        var signedAt = material.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        var algorithm = GetAlgorithmDisplayName(material.Mechanism);

        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 49, "Signed by", requestedBy, 24);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 64, "Certificate", certificateName, 22);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 79, "Date", signedAt, 30);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 94, "Algorithm", algorithm, 20);
    }

    private static void DrawInfoRow(
        XGraphics gfx,
        XFont labelFont,
        XFont valueFont,
        XBrush labelBrush,
        XBrush valueBrush,
        XRect container,
        double y,
        string label,
        string value,
        int maxLength)
    {
        gfx.DrawString(label, labelFont, labelBrush, new XPoint(container.X + 14, container.Y + y));
        gfx.DrawString(Truncate(value, maxLength), valueFont, valueBrush, new XPoint(container.X + 76, container.Y + y));
    }

    private static void ApplyPageOrientationTransform(XGraphics gfx, PdfPage page)
    {
        var rotation = NormalizeRotation(page.Rotate);
        switch (rotation)
        {
            case 90:
                gfx.RotateAtTransform(90, new XPoint(0, 0));
                gfx.TranslateTransform(0, -page.Width.Point);
                break;
            case 180:
                gfx.RotateAtTransform(180, new XPoint(0, 0));
                gfx.TranslateTransform(-page.Width.Point, -page.Height.Point);
                break;
            case 270:
                gfx.RotateAtTransform(270, new XPoint(0, 0));
                gfx.TranslateTransform(-page.Height.Point, 0);
                break;
        }
    }

    private static double GetOrientedPageWidth(PdfPage page)
    {
        var rotation = NormalizeRotation(page.Rotate);
        return rotation is 90 or 270 ? page.Height.Point : page.Width.Point;
    }

    private static double GetOrientedPageHeight(PdfPage page)
    {
        var rotation = NormalizeRotation(page.Rotate);
        return rotation is 90 or 270 ? page.Width.Point : page.Height.Point;
    }

    private static int NormalizeRotation(int rotate)
    {
        var normalized = rotate % 360;
        if (normalized < 0)
            normalized += 360;

        return normalized;
    }

    private static string GetCertificateDisplayName(byte[] leafDer)
    {
        var certificate = new X509Certificate2(leafDer);
        var subject = certificate.SubjectName.Name ?? certificate.Subject;
        if (string.IsNullOrWhiteSpace(subject))
            return "Unknown certificate";

        var match = Regex.Match(subject, @"(?:^|,\s*)CN=(?<value>[^,]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : subject;
    }

    private static string GetAlgorithmDisplayName(Mechanism mechanism) =>
        mechanism switch
        {
            var m when m == Mechanism.RsaPssSha256 => "RSA-PSS SHA-256",
            var m when m == Mechanism.EcdsaP256Sha256Der => "ECDSA P-256 SHA-256",
            _ => mechanism.Name
        };

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        if (maxLength <= 3)
            return value[..maxLength];

        return value[..(maxLength - 3)] + "...";
    }
}
