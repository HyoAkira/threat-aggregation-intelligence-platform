using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.BaseUrl)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.IsEnabled).IsRequired();
        builder.Property(p => p.IntervalMinutes).IsRequired();

        builder.Property(p => p.DefaultMode)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(p => p.SupportedModes)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.RecentCooldownMinutes);
        builder.Property(p => p.FullCooldownMinutes);
        builder.Property(p => p.MinimumIntervalMinutes).IsRequired();
        builder.Property(p => p.RequiresApiKey).IsRequired();

        builder.Property(p => p.LastFetchStatus)
            .HasConversion<byte?>();

        builder.Property(p => p.LastFetchAt).HasColumnType("datetime2");
        builder.Property(p => p.NextFetchTime).HasColumnType("datetime2");

        builder.HasMany<IoC>()
            .WithOne()
            .HasForeignKey(i => i.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<FetchHistory>()
            .WithOne()
            .HasForeignKey(f => f.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
