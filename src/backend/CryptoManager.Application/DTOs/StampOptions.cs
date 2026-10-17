namespace CryptoManager.Application.DTOs;

/// <summary>
/// Controls whether and where the visible signature stamp is drawn on a signed PDF.
/// Coordinates are 0–1 normalised with top-left origin (frontend convention);
/// the PDF layer converts them to PDF points (bottom-left origin).
/// </summary>
public sealed record StampOptions(
    bool Enabled,
    float? X,
    float? Y,
    float? Width,
    float? Height,
    float RotationDegrees);
