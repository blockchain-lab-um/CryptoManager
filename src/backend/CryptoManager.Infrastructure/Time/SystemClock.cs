using CryptoManager.Application.Abstractions;

namespace CryptoManager.Infrastructure.Time
{
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
