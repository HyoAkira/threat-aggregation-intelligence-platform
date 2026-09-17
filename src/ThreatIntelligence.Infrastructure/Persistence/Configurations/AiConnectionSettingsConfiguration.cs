using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class AiConnectionSettingsConfiguration : IEntityTypeConfiguration<AiConnectionSettings>
{
    public void Configure(EntityTypeBuilder<AiConnectionSettings> builder)
    {
        builder.ToTable("aiConnectionSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.BaseUrl).HasMaxLength(2048);
        builder.Property(settings => settings.ModelId).HasMaxLength(200);
        builder.Property(settings => settings.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(settings => settings.Version).IsRowVersion();
    }
}
