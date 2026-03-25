using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

/// <summary>
/// SHA-256 certificate thumbprint stored as uppercase hex (64 characters).
/// </summary>
public readonly record struct Thumbprint
{
    public string Value { get; }

    public Thumbprint(string value)
    {
        Guard.NotEmpty(value, nameof(value));
        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;
}