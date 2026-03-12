namespace CryptoManager.Application.Exceptions;

public sealed class HsmUnavailableException : Exception
{
    public HsmUnavailableException(string message) : base(message) { }
    public HsmUnavailableException(string message, Exception inner) : base(message, inner) { }
}