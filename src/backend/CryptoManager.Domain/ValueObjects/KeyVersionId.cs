namespace CryptoManager.Domain.ValueObjects;

public readonly record struct KeyVersionId(Guid Value)
{
    public static KeyVersionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
