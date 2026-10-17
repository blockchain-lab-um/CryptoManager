namespace CryptoManager.Application.Abstractions;

public interface ICurrentUser
{
    /// <summary>Username — used as the actor string in audit events.</summary>
    string Actor { get; }

    /// <summary>Subject claim (user ID) — used for key ownership checks.</summary>
    string UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}