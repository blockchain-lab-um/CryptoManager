using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, g => new AuditEventId(g))
            .HasColumnName("Id");

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.Actor)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.ActorId)
            .HasMaxLength(450); // matches ASP.NET Identity PK length

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        // Nullable KeyId (record struct wrapping Guid).
        builder.Property(a => a.KeyId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                g => g.HasValue ? new KeyId(g.Value) : (KeyId?)null)
            .HasColumnName("KeyId");

        builder.Property(a => a.KeyVersion);

        builder.Property(a => a.Mechanism)
            .HasMaxLength(100);

        builder.Property(a => a.RequestId)
            .HasMaxLength(100);

        builder.Property(a => a.Success)
            .IsRequired();

        builder.Property(a => a.Error)
            .HasMaxLength(2000);

        builder.Property(a => a.CertificateId);

        builder.Property(a => a.CertificateThumbprint)
            .HasMaxLength(128);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.KeyId);
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => new { a.ActorId, a.Timestamp }); // hot path: per-user + date-range queries
        builder.HasIndex(a => a.Actor);                        // admin by-username filter
    }
}
