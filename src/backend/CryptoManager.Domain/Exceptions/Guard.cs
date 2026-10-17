namespace CryptoManager.Domain.Exceptions;

public static class Guard
{
    public static void NotNull(object? value, string name)
    {
        if (value is null) throw new DomainException($"{name} must not be null.");
    }

    public static void NotEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{name} must not be empty.");
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new DomainException(message);
    }
}
