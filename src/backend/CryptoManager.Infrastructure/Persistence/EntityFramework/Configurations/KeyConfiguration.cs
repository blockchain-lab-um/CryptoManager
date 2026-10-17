using System.Text.Json;
using CryptoManager.Domain.Entities;
using CryptoManager.Domain.Enums;
using CryptoManager.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class KeyConfiguration : IEntityTypeConfiguration<Key>
{
    public void Configure(EntityTypeBuilder<Key> builder)
    {
        builder.ToTable("Keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasConversion(id => id.Value, g => new KeyId(g))
            .HasColumnName("Id");

        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(k => k.Purpose)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(k => k.State)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .IsRequired();

        builder.Property(k => k.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(k => k.OwnerId)
            .IsRequired()
            .HasMaxLength(450); // matches ASP.NET Identity key length

        // AllowedMechanisms is backed by private field _allowedMechanismNames (HashSet<string>).
        // Serialise as a JSON array column.
        var jsonOptions = new JsonSerializerOptions();
        builder.Property<HashSet<string>>("_allowedMechanismNames")
            .HasField("_allowedMechanismNames")
            .HasColumnName("AllowedMechanisms")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<HashSet<string>>(v, jsonOptions)!
                         ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new ValueComparer<HashSet<string>>(
                    (a, b) => a != null && b != null && a.SetEquals(b),
                    v => v.Aggregate(0, (acc, e) => HashCode.Combine(acc, StringComparer.OrdinalIgnoreCase.GetHashCode(e))),
                    v => new HashSet<string>(v, StringComparer.OrdinalIgnoreCase)));

        builder.HasMany<KeyVersion>(nameof(Key.Versions))
            .WithOne()
            .HasForeignKey(kv => kv.KeyId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(nameof(Key.Versions))
            .HasField("_versions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(k => new { k.Name, k.OwnerId }).IsUnique();
    }
}
