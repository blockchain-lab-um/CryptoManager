namespace CryptoManager.Domain.ValueObjects;

public readonly record struct KeyId(Guid Value)
{
    public static KeyId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
