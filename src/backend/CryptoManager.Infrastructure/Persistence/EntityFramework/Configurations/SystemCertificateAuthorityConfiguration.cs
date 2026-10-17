using CryptoManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class SystemCertificateAuthorityConfiguration : IEntityTypeConfiguration<SystemCertificateAuthority>
{
    public void Configure(EntityTypeBuilder<SystemCertificateAuthority> builder)
    {
        builder.ToTable("SystemCertificateAuthorities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CertificateDer)
            .IsRequired();

        builder.Property(x => x.SubjectDn)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Thumbprint)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SerialNumber)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.OwnsOne(x => x.ProviderRef, pr =>
        {
            pr.Property(p => p.ProviderType)
                .HasColumnName("ProviderType")
                .HasMaxLength(50)
                .IsRequired();

            pr.Property(p => p.ProviderInstanceId)
                .HasColumnName("ProviderInstanceId")
                .HasMaxLength(100)
                .IsRequired();

            pr.Property(p => p.Reference)
                .HasColumnName("ProviderRef")
                .HasMaxLength(500)
                .IsRequired();
        });

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
