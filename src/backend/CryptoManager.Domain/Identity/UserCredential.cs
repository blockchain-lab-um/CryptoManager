namespace CryptoManager.Domain.Identity;

public sealed class UserCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public byte[] CredentialId { get; set; } = default!;
    public byte[] PublicKey { get; set; } = default!;
    public byte[] UserHandle { get; set; } = default!;
    public uint SignCount { get; set; }
    public string? AttestationFormat { get; set; }
    public Guid? AaGuid { get; set; }
    public string[] Transports { get; set; } = Array.Empty<string>();
    public bool IsBackupEligible { get; set; }
    public bool IsBackedUp { get; set; }
    public string Nickname { get; set; } = "Passkey";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}