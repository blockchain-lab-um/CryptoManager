using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

/// <summary>
/// Represents an X.500 Distinguished Name.
/// Use <see cref="From"/> to build from named components; use the string constructor
/// when parsing a DN from an existing certificate.
/// </summary>
public sealed record SubjectDN
{
    public string Value { get; }

    public SubjectDN(string value)
    {
        Guard.NotEmpty(value, nameof(value));
        Value = value.Trim();
    }

    /// <summary>
    /// Builds an RFC 4514 DN string from named components.
    /// At least <paramref name="cn"/> must be provided.
    /// </summary>
    public static SubjectDN From(string cn, string? o = null, string? ou = null, string? c = null)
    {
        Guard.NotEmpty(cn, nameof(cn));

        var parts = new List<string> { $"CN={Escape(cn)}" };
        if (!string.IsNullOrWhiteSpace(ou)) parts.Add($"OU={Escape(ou!)}");
        if (!string.IsNullOrWhiteSpace(o))  parts.Add($"O={Escape(o!)}");
        if (!string.IsNullOrWhiteSpace(c))  parts.Add($"C={Escape(c!)}");

        return new SubjectDN(string.Join(", ", parts));
    }

    public override string ToString() => Value;

    // Minimal RFC 4514 escaping for special characters.
    private static string Escape(string value)
    {
        // Characters that must be escaped in RFC 4514 attribute values.
        return value
            .Replace("\\", "\\\\")
            .Replace(",",  "\\,")
            .Replace("+",  "\\+")
            .Replace("\"", "\\\"")
            .Replace("<",  "\\<")
            .Replace(">",  "\\>")
            .Replace(";",  "\\;")
            .Replace("#",  "\\#")
            .Replace("=",  "\\=");
    }
}