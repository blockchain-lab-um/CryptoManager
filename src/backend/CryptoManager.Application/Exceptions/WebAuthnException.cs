namespace CryptoManager.Application.Exceptions;

public sealed class WebAuthnException : Exception
{
    public WebAuthnException(string message) : base(message) { }
}
