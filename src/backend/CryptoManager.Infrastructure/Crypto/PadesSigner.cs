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
        StampOptions? stamp,
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

            AddVisibleSignatureStamp(document, material, stamp);

            DigitalSignatureHandler.ForDocument(document, signer, new DigitalSignatureOptions());

            document.Info.ModificationDate = originalModDate;
            document.Save(outputStream, closeStream: false);
            signed = outputStream.ToArray();
        }

        var outputFileName = Path.GetFileNameWithoutExtension(originalFileName) + "_signed.pdf";
        return await Task.FromResult((outputFileName, signed));
    }

    private static void AddVisibleSignatureStamp(PdfDocument document, DocumentSigningMaterial material, StampOptions? stamp)
    {
        var enabled = stamp?.Enabled ?? true;
        if (!enabled)
            return;

        if (document.PageCount == 0)
            return;

        var page = document.Pages[0];
        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

        const double defaultBoxWidth = 232;
        const double defaultBoxHeight = 108;
        const double margin = 24;

        ApplyPageOrientationTransform(gfx, page);

        var orientedWidth = GetOrientedPageWidth(page);
        var orientedHeight = GetOrientedPageHeight(page);
        var pageRotation = NormalizeRotation(page.Rotate);

        XRect rect;
        double visualRotationDegrees = stamp?.RotationDegrees ?? 0f;

        if (HasValidCustomStampBounds(stamp))
        {
            double visualWidth = stamp.Width!.Value * orientedWidth;
            double visualHeight = stamp.Height!.Value * orientedHeight;
            double visualX = stamp.X!.Value * orientedWidth;
            double visualY = stamp.Y!.Value * orientedHeight;
            rect = MapVisualRectToDrawingRect(pageRotation, orientedWidth, orientedHeight, visualX, visualY, visualWidth, visualHeight);
        }
        else
        {
            var visualX = orientedWidth - defaultBoxWidth - margin;
            var visualY = orientedHeight - defaultBoxHeight - margin;
            rect = MapVisualRectToDrawingRect(pageRotation, orientedWidth, orientedHeight, visualX, visualY, defaultBoxWidth, defaultBoxHeight);
        }

        var scale = Math.Min(rect.Width / defaultBoxWidth, rect.Height / defaultBoxHeight);
        var radius = 10 * scale;
        var panelBrush = new XSolidBrush(XColor.FromArgb(248, 248, 248));
        var borderPen = new XPen(XColor.FromArgb(44, 44, 44), Math.Max(0.6, 1.2 * scale));
        var accentBrush = new XSolidBrush(XColor.FromArgb(28, 28, 28));
        var mutedBrush = new XSolidBrush(XColor.FromArgb(96, 96, 96));
        var linePen = new XPen(XColor.FromArgb(214, 214, 214), Math.Max(0.5, 1 * scale));
        var badgeBrush = new XSolidBrush(XColor.FromArgb(32, 32, 32));
        var badgeTextBrush = XBrushes.White;
        var iconPen = new XPen(XColor.FromArgb(255, 255, 255), Math.Max(0.9, 2.2 * scale))
        {
            LineCap = XLineCap.Round
        };

        var titleFont = new XFont("Switzer", 10 * scale, XFontStyleEx.Bold);
        var bodyFont = new XFont("Switzer", 8.5 * scale, XFontStyleEx.Regular);
        var labelFont = new XFont("Switzer", 7.5 * scale, XFontStyleEx.Bold);

        var requestedBy = string.IsNullOrWhiteSpace(material.RequestedBy) ? "Unknown user" : material.RequestedBy;
        var certificateName = GetCertificateDisplayName(material.CertBundle.LeafDer);
        var signedAt = material.SignedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        var algorithm = GetAlgorithmDisplayName(material.Mechanism);
        var drawingRotationDegrees = MapVisualRotationToDrawingRotation(pageRotation, visualRotationDegrees);

        var state = gfx.Save();
        if (Math.Abs(drawingRotationDegrees) > 0.001)
        {
            var center = new XPoint(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            gfx.TranslateTransform(center.X, center.Y, XMatrixOrder.Append);
            gfx.RotateTransform(drawingRotationDegrees, XMatrixOrder.Append);
            gfx.TranslateTransform(-center.X, -center.Y, XMatrixOrder.Append);
        }

        gfx.DrawRoundedRectangle(borderPen, panelBrush, rect, new XSize(radius, radius));

        var leftPadding = 14 * scale;
        var rightPadding = 14 * scale;
        var topPadding = 10 * scale;
        var badgeHeight = 18 * scale;
        var iconSize = 20 * scale;
        var iconGap = 8 * scale;
        var badgeMaxWidth = Math.Max(24 * scale, rect.Width - leftPadding - rightPadding - iconSize - iconGap);
        var badgeWidth = Math.Min(104 * scale, badgeMaxWidth);
        var badgeRect = new XRect(rect.X + 12 * scale, rect.Y + topPadding, badgeWidth, badgeHeight);
        gfx.DrawRoundedRectangle(XPens.Transparent, badgeBrush, badgeRect, new XSize(9, 9));
        gfx.DrawString(FitText(gfx, "DIGITALLY SIGNED", labelFont, badgeRect.Width - 10 * scale), labelFont, badgeTextBrush, badgeRect, XStringFormats.Center);

        var iconCenterX = rect.Right - rightPadding - iconSize / 2;
        var iconCenterY = rect.Y + topPadding + badgeHeight / 2;
        var iconRect = new XRect(iconCenterX - iconSize / 2, iconCenterY - iconSize / 2, iconSize, iconSize);
        gfx.DrawEllipse(XPens.Transparent, accentBrush, iconRect);
        gfx.DrawLine(iconPen, iconCenterX - 4 * scale, iconCenterY + 1 * scale, iconCenterX - 1 * scale, iconCenterY + 4 * scale);
        gfx.DrawLine(iconPen, iconCenterX - 1 * scale, iconCenterY + 4 * scale, iconCenterX + 5 * scale, iconCenterY - 3 * scale);

        var dividerY = rect.Y + 34 * scale;
        gfx.DrawLine(linePen, rect.X + 12 * scale, dividerY, rect.Right - 12 * scale, dividerY);

        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 49 * scale, "Signed by", requestedBy, scale);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 64 * scale, "Certificate", certificateName, scale);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 79 * scale, "Date", signedAt, scale);
        DrawInfoRow(gfx, labelFont, bodyFont, mutedBrush, accentBrush, rect, 94 * scale, "Algorithm", algorithm, scale);
        gfx.Restore(state);
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
        double scale)
    {
        var labelX = container.X + 14 * scale;
        var valueX = container.X + 76 * scale;
        var baselineY = container.Y + y;
        var maxValueWidth = Math.Max(12 * scale, container.Right - valueX - 14 * scale);

        gfx.DrawString(label, labelFont, labelBrush, new XPoint(labelX, baselineY));
        gfx.DrawString(FitText(gfx, value, valueFont, maxValueWidth), valueFont, valueBrush, new XPoint(valueX, baselineY));
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

    private static XRect MapVisualRectToDrawingRect(
        int pageRotation,
        double orientedWidth,
        double orientedHeight,
        double x,
        double y,
        double width,
        double height)
    {
        return pageRotation switch
        {
            0 => new XRect(x, y, width, height),
            90 => new XRect(x, orientedHeight - y - height, width, height),
            180 => new XRect(orientedWidth - x - width, orientedHeight - y - height, width, height),
            270 => new XRect(x, orientedHeight - y - height, width, height),
            _ => new XRect(x, y, width, height),
        };
    }

    private static double MapVisualRotationToDrawingRotation(int pageRotation, double rotationDegrees)
    {
        return rotationDegrees;
    }

    private static bool HasValidCustomStampBounds(StampOptions? stamp)
    {
        if (stamp?.X.HasValue != true || !stamp.Y.HasValue || !stamp.Width.HasValue || !stamp.Height.HasValue)
            return false;

        return IsBetweenInclusive(stamp.X.Value, 0f, 1f)
               && IsBetweenInclusive(stamp.Y.Value, 0f, 1f)
               && IsBetweenExclusive(stamp.Width.Value, 0f, 1f)
               && IsBetweenExclusive(stamp.Height.Value, 0f, 1f);
    }

    private static bool IsBetweenInclusive(float value, float min, float max) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

    private static bool IsBetweenExclusive(float value, float min, float max) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > min && value <= max;

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

    private static string FitText(XGraphics gfx, string value, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(value) || maxWidth <= 0)
            return value;

        if (gfx.MeasureString(value, font).Width <= maxWidth)
            return value;

        const string ellipsis = "...";
        if (gfx.MeasureString(ellipsis, font).Width > maxWidth)
            return string.Empty;

        var low = 0;
        var high = value.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidate = value[..mid] + ellipsis;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return low <= 0 ? ellipsis : value[..low] + ellipsis;
    }
}
