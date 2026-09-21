using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        builder.ToTable("settings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Value)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(s => s.UpdatedAt).HasColumnType("datetime2").IsRequired();

        builder.HasIndex(s => s.Key)
            .IsUnique()
            .HasDatabaseName("IX_settings_Key");
    }
}