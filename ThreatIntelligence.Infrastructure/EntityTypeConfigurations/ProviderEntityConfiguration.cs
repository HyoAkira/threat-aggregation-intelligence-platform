using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.EntityTypeConfigurations;

public class ProviderEntityConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
        builder.Property(p => p.BaseUrl).HasColumnName("baseUrl").IsRequired().HasMaxLength(255);
        builder.Property(p => p.IsEnabled).HasColumnName("isEnabled").IsRequired();
        builder.Property(p => p.IntervalMinutes).HasColumnName("intervalMinutes").IsRequired();

        builder.HasMany(p => p.Iocs)
            .WithOne(i => i.Provider)
            .HasForeignKey(i => i.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new Provider
            {
                Id = 1,
                Name = "ThreatFox",
                BaseUrl = "https://threatfox-api.abuse.ch",
                IsEnabled = true,
                IntervalMinutes = 5
            },
            new Provider
            {
                Id = 2,
                Name = "PhishTank",
                BaseUrl = "http://data.phishtank.com/data/",
                IsEnabled = true,
                IntervalMinutes = 60
            });
    }
}
