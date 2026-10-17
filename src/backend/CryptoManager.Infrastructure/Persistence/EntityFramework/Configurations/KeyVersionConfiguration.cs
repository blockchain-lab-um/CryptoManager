using CryptoManager.Domain.Entities;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class KeyVersionConfiguration : IEntityTypeConfiguration<KeyVersion>
{
    public void Configure(EntityTypeBuilder<KeyVersion> builder)
    {
        builder.ToTable("KeyVersions");

        builder.HasKey(kv => kv.Id);

        builder.Property(kv => kv.Id)
            .HasConversion(id => id.Value, g => new KeyVersionId(g))
            .HasColumnName("Id");

        // FK back to Key — KeyId is a struct value object wrapping Guid.
        builder.Property(kv => kv.KeyId)
            .HasConversion(id => id.Value, g => new KeyId(g))
            .HasColumnName("KeyId")
            .IsRequired();

        builder.Property(kv => kv.Version)
            .IsRequired();

        builder.Property(kv => kv.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(kv => kv.CreatedAt)
            .IsRequired();

        builder.Property(kv => kv.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        // ProviderRef — owned entity, two columns on the same table.
        builder.OwnsOne(kv => kv.ProviderRef, pr =>
        {
            pr.Property(p => p.ProviderInstanceId)
                .HasColumnName("ProviderInstanceId")
                .HasMaxLength(100)
                .IsRequired();

            pr.Property(p => p.ProviderType)
                .HasColumnName("ProviderType")
                .HasMaxLength(50)
                .IsRequired();

            pr.Property(p => p.Reference)
                .HasColumnName("ProviderRef")
                .HasMaxLength(500)
                .IsRequired();
        });

        // PublicKeyMaterial — single PEM column.
        builder.OwnsOne(kv => kv.PublicKey, pk =>
        {
            pk.Property(p => p.Pem)
                .HasColumnName("PublicKeyPem")
                .IsRequired();
        });

        builder.HasIndex(kv => new { kv.KeyId, kv.Version }).IsUnique();
    }
}
