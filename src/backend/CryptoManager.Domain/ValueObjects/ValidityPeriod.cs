using CryptoManager.Domain.Exceptions;

namespace CryptoManager.Domain.ValueObjects;

public readonly record struct ValidityPeriod
{
    public DateTimeOffset NotBefore { get; init; }
    public DateTimeOffset NotAfter  { get; init; }

    public ValidityPeriod(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        Guard.True(notAfter > notBefore, "NotAfter must be later than NotBefore.");
        NotBefore = notBefore;
        NotAfter  = notAfter;
    }

    public bool IsExpired(DateTimeOffset now) => now >= NotAfter;

    public bool ExpiresWithin(int days, DateTimeOffset now) =>
        !IsExpired(now) && NotAfter <= now.AddDays(days);
}