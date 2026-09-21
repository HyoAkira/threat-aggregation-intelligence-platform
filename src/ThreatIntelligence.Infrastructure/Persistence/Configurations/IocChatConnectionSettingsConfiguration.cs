using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class IocChatConnectionSettingsConfiguration : IEntityTypeConfiguration<IocChatConnectionSettings>
{
    public void Configure(EntityTypeBuilder<IocChatConnectionSettings> builder)
    {
        builder.ToTable("iocChatConnectionSettings");
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