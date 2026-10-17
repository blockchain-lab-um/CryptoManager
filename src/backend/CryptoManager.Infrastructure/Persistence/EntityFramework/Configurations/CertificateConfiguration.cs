using System.Text.Json;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("Certificates");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, g => new CertificateId(g))
            .HasColumnName("Id");

        builder.Property(c => c.KeyVersionId)
            .HasConversion(id => id.Value, g => new KeyVersionId(g))
            .HasColumnName("KeyVersionId")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Source)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.EnrollmentId)
            .HasMaxLength(500);

        builder.Property(c => c.SerialNumber)
            .HasMaxLength(200);

        builder.Property(c => c.Thumbprint)
            .HasMaxLength(128);  // SHA-256 hex = 64 chars; room for other algorithms

        builder.Property(c => c.SubjectDN)
            .HasMaxLength(1000);

        builder.Property(c => c.IssuerDN)
            .HasMaxLength(1000);

        builder.Property(c => c.NotBefore);
        builder.Property(c => c.NotAfter);

        // DER-encoded leaf certificate stored as bytea.
        builder.Property(c => c.CertificateDer)
            .HasColumnType("bytea");

        // Chain as a JSON array of base64-encoded DER certificates.
        builder.Property(c => c.ChainDer)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null
                    ? null
                    : JsonSerializer.Serialize(v.Select(Convert.ToBase64String).ToArray(), JsonOptions),
                v => v == null
                    ? null
                    : JsonSerializer.Deserialize<string[]>(v, JsonOptions)!
                        .Select(Convert.FromBase64String)
                        .ToArray(),
                new ValueComparer<byte[][]>(
                    (a, b) => a != null && b != null && a.Length == b.Length &&
                              a.Zip(b).All(pair => pair.First.SequenceEqual(pair.Second)),
                    v => v.Aggregate(0, (acc, item) => HashCode.Combine(acc, item.Length)),
                    v => v.Select(b => b.ToArray()).ToArray()));

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        // Unique partial index: at most one Active certificate per KeyVersion.
        // PostgreSQL NULLs are DISTINCT in unique indexes, so Status=Active is the only
        // value that can appear once per KeyVersionId. All other statuses are unrestricted.
        builder.HasIndex(c => c.KeyVersionId)
            .HasFilter("\"Status\" = 'Active'")
            .IsUnique()
            .HasDatabaseName("ix_certificates_active_per_keyversion");

        // Unique index on Thumbprint, excluding NULLs (PendingEnrollment has no thumbprint yet).
        builder.HasIndex(c => c.Thumbprint)
            .IsUnique()
            .HasFilter("\"Thumbprint\" IS NOT NULL")
            .HasDatabaseName("ix_certificates_thumbprint");

        // Query index: all certs for a given KeyVersion.
        builder.HasIndex(c => c.KeyVersionId)
            .HasDatabaseName("ix_certificates_keyversion");
    }
}