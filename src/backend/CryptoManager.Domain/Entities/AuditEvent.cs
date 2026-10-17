using CryptoManager.Domain.Enums;
using CryptoManager.Domain.Exceptions;
using CryptoManager.Domain.ValueObjects;

namespace CryptoManager.Domain.Entities;

public sealed class AuditEvent
{
    public AuditEventId Id { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    public string Actor { get; private set; } = default!; // subject/user/service principal
    public string? ActorId { get; private set; }          // stable identity key (NameIdentifier claim)
    public AuditAction Action { get; private set; }

    public KeyId? KeyId { get; private set; }
    public int? KeyVersion { get; private set; }
    public string? Mechanism { get; private set; } // store as string name for stability

    public string? RequestId { get; private set; }
    public bool Success { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Populated for certificate-lifecycle and document-signing events.</summary>
    public Guid? CertificateId { get; private set; }

    /// <summary>
    /// Denormalized SHA-256 thumbprint of the certificate, so audit records are
    /// self-contained without joining to the Certificates table.
    /// </summary>
    public string? CertificateThumbprint { get; private set; }

    private AuditEvent() { }

    public AuditEvent(
        AuditEventId id,
        DateTimeOffset timestamp,
        string actor,
        string? actorId,
        AuditAction action,
        KeyId? keyId,
        int? keyVersion,
        Mechanism? mechanism,
        string? requestId,
        bool success,
        string? error,
        Guid? certificateId = null,
        string? certificateThumbprint = null)
    {
        Guard.True(id.Value != Guid.Empty, "AuditEventId must not be empty.");
        Guard.NotEmpty(actor, nameof(actor));

        Id                   = id;
        Timestamp            = timestamp;
        Actor                = actor;
        ActorId              = actorId;
        Action               = action;
        KeyId                = keyId;
        KeyVersion           = keyVersion;
        Mechanism            = mechanism?.Name;
        RequestId            = requestId;
        Success              = success;
        Error                = error;
        CertificateId        = certificateId;
        CertificateThumbprint = certificateThumbprint;
    }
}
