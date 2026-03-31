using PdfSharp.Fonts;

namespace CryptoManager.Infrastructure.Pdf;

public sealed class SwitzerFontResolver : IFontResolver
{
    private const string FamilyName = "Switzer";
    private const string RegularFace = "Switzer-Regular";
    private const string SemiboldFace = "Switzer-Semibold";
    private const string ItalicFace = "Switzer-Italic";
    private const string SemiboldItalicFace = "Switzer-SemiboldItalic";

    private readonly string _fontDirectory;
    private readonly Dictionary<string, string> _fontFiles;

    public SwitzerFontResolver(string fontDirectory)
    {
        _fontDirectory = fontDirectory;
        _fontFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RegularFace] = Path.Combine(_fontDirectory, "Switzer-Regular.ttf"),
            [SemiboldFace] = Path.Combine(_fontDirectory, "Switzer-Semibold.ttf"),
            [ItalicFace] = Path.Combine(_fontDirectory, "Switzer-Italic.ttf"),
            [SemiboldItalicFace] = Path.Combine(_fontDirectory, "Switzer-SemiboldItalic.ttf")
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
