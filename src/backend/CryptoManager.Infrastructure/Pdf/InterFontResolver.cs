using PdfSharp.Fonts;

namespace CryptoManager.Infrastructure.Pdf;

public sealed class InterFontResolver : IFontResolver
{
    public const string FamilyName = "Inter";
    private const string RegularFace = "Inter-Regular";
    private const string SemiboldFace = "Inter-SemiBold";
    private const string ItalicFace = "Inter-Italic";
    private const string SemiboldItalicFace = "Inter-SemiBoldItalic";

    private readonly string _fontDirectory;
    private readonly Dictionary<string, string> _fontFiles;

    public InterFontResolver(string fontDirectory)
    {
        _fontDirectory = fontDirectory;
        _fontFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RegularFace] = Path.Combine(_fontDirectory, "Inter-Regular.ttf"),
            [SemiboldFace] = Path.Combine(_fontDirectory, "Inter-SemiBold.ttf"),
            [ItalicFace] = Path.Combine(_fontDirectory, "Inter-Italic.ttf"),
            [SemiboldItalicFace] = Path.Combine(_fontDirectory, "Inter-SemiBoldItalic.ttf")
        };
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase))
        {
            if (bold && italic)
                return new FontResolverInfo(SemiboldItalicFace);
            if (bold)
                return new FontResolverInfo(SemiboldFace);
            if (italic)
                return new FontResolverInfo(ItalicFace);

            return new FontResolverInfo(RegularFace);
        }

        return PlatformFontResolver.ResolveTypeface(familyName, bold, italic);
    }

    public byte[] GetFont(string faceName)
    {
        if (!_fontFiles.TryGetValue(faceName, out var path))
            throw new InvalidOperationException($"Unknown font face '{faceName}'.");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Font file '{path}' was not found.", path);

        return File.ReadAllBytes(path);
    }
}
