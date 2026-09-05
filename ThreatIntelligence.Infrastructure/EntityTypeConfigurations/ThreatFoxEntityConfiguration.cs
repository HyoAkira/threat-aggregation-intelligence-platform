using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services.EntityTypeConfigurations;

public class ThreatFoxEntityConfiguration : IEntityTypeConfiguration<ThreatFox>
{
    public void Configure(EntityTypeBuilder<ThreatFox> builder)
    {
        builder.ToTable("ThreatFox", "providers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.IocId).IsRequired();
        builder.Property(t => t.IocValue).IsRequired().HasMaxLength(500);
        builder.Property(t => t.IocType).IsRequired().HasMaxLength(50);
        builder.Property(t => t.ThreatType).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Malware).IsRequired().HasMaxLength(200);
        builder.Property(t => t.MalwareAlias).HasMaxLength(500);
        builder.Property(t => t.MalwarePrintable).IsRequired().HasMaxLength(200);
        builder.Property(t => t.FirstSeenUtc).IsRequired();
        builder.Property(t => t.LastSeenUtc);
        builder.Property(t => t.ConfidenceLevel).IsRequired();
        builder.Property(t => t.IsCompromised).IsRequired();
        builder.Property(t => t.Reference).HasColumnType("nvarchar(max)");
        builder.Property(t => t.Tags).HasColumnType("nvarchar(max)");
        builder.Property(t => t.Anonymous).IsRequired();
        builder.Property(t => t.Reporter).IsRequired().HasMaxLength(200);

        builder.HasIndex(t => new { t.IocId, t.IocValue, t.IocType }).IsUnique();
    }
}
