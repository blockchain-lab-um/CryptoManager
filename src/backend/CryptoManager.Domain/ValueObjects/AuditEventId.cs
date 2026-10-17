namespace CryptoManager.Domain.ValueObjects;

public readonly record struct AuditEventId(Guid Value)
{
    public static AuditEventId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
