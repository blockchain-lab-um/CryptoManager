using CryptoManager.Domain.Identity;
using CryptoManager.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CryptoManager.Infrastructure.Persistence.EntityFramework.Configurations;

internal sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> b)
    {
        b.ToTable("UserCredentials");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.CredentialId).IsRequired();
        b.HasIndex(x => x.CredentialId).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Property(x => x.PublicKey).IsRequired();
        b.Property(x => x.UserHandle).IsRequired();
        b.Property(x => x.Transports).HasColumnType("text[]");
        b.Property(x => x.Nickname).HasMaxLength(120).IsRequired();
        b.Property(x => x.AttestationFormat).HasMaxLength(40);
        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
