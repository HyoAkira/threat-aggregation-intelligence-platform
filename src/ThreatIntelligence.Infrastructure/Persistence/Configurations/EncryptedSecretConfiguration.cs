using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class EncryptedSecretConfiguration : IEntityTypeConfiguration<EncryptedSecret>
{
    public void Configure(EntityTypeBuilder<EncryptedSecret> builder)
    {
        builder.ToTable("encryptedSecrets");
        builder.HasKey(secret => secret.Id);
        builder.Property(secret => secret.Name).HasMaxLength(100).IsRequired();
        builder.Property(secret => secret.FormatVersion).IsRequired();
        builder.Property(secret => secret.KeyVersion).IsRequired();
        builder.Property(secret => secret.Ciphertext).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(secret => secret.Nonce).HasColumnType("binary(12)").IsRequired();
        builder.Property(secret => secret.AuthenticationTag).HasColumnType("binary(16)").IsRequired();
        // SQL Server datetime2 does not persist DateTimeKind. Re-apply the UTC contract when
        // materializing persisted timestamps so replacing an existing secret does not fail the
        // domain entity's UTC invariant.
        builder.Property(secret => secret.CreatedAt)
            .HasColumnType("datetime2")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(secret => secret.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(secret => secret.Version).IsRowVersion();
        builder.HasIndex(secret => secret.Name).IsUnique().HasDatabaseName("IX_encryptedSecrets_Name");
    }
}
