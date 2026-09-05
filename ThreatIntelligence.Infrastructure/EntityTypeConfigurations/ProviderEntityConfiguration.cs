using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services.EntityTypeConfigurations;

public class ProviderEntityConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("Provider", "threat");

        builder.HasKey(p => p.ProviderId);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.BaseUrl).IsRequired().HasMaxLength(500);
        builder.Property(p => p.IsEnabled).IsRequired();
        builder.Property(p => p.FetchIntervalMinutes).IsRequired();
    }
}
