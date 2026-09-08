using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.EntityTypeConfigurations;

public class IocEntityConfiguration : IEntityTypeConfiguration<Ioc>
{
    public void Configure(EntityTypeBuilder<Ioc> builder)
    {
        builder.ToTable("iocs", table =>
        {
            table.HasCheckConstraint("CK_iocs_port", "[port] IS NULL OR [port] BETWEEN 1 AND 65535");
            table.HasCheckConstraint("CK_iocs_threatFoxConfidenceLevel", "[threatFoxConfidenceLevel] IS NULL OR [threatFoxConfidenceLevel] BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_iocs_phishTankDetails", "[phishTankDetails] IS NULL OR ISJSON([phishTankDetails]) = 1");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ProviderId).HasColumnName("providerId");
        builder.Property(i => i.ProviderRecordId).HasColumnName("providerRecordId").IsRequired().HasMaxLength(100);
        builder.Property(i => i.ProviderDetailsLink).HasColumnName("providerDetailsLink").HasMaxLength(2048);
        builder.Property(i => i.IocValue).HasColumnName("iocValue").IsRequired().HasMaxLength(2048);
        builder.Property(i => i.IocValueHash).HasColumnName("iocValueHash").IsRequired().HasColumnType("binary(32)");
        builder.Property(i => i.IocType).HasColumnName("iocType").HasConversion<byte>();
        builder.Property(i => i.Port).HasColumnName("port");
        builder.Property(i => i.ThreatType).HasColumnName("threatType").HasMaxLength(100);
        builder.Property(i => i.Malware).HasColumnName("malware").HasMaxLength(200);
        builder.Property(i => i.MalwareAlias).HasColumnName("malwareAlias").HasMaxLength(500);
        builder.Property(i => i.MalwarePrintable).HasColumnName("malwarePrintable").HasMaxLength(200);
        builder.Property(i => i.FirstSeenUtc).HasColumnName("firstSeenUtc");
        builder.Property(i => i.LastSeenUtc).HasColumnName("lastSeenUtc");
        builder.Property(i => i.IsCompromised).HasColumnName("isCompromised");
        builder.Property(i => i.Reporter).HasColumnName("reporter").HasMaxLength(200);
        builder.Property(i => i.Anonymous).HasColumnName("anonymous");
        builder.Property(i => i.IsVerified).HasColumnName("isVerified");
        builder.Property(i => i.Target).HasColumnName("target").HasMaxLength(500);
        builder.Property(i => i.PhishTankDetails).HasColumnName("phishTankDetails").HasColumnType("nvarchar(max)");
        builder.Property(i => i.PhishTankOnline).HasColumnName("phishTankOnline");
        builder.Property(i => i.UrlHost).HasColumnName("urlHost").HasMaxLength(253);
        builder.Property(i => i.ParentDomain).HasColumnName("parentDomain").HasMaxLength(253);
        builder.Property(i => i.ThreatFoxConfidenceLevel).HasColumnName("threatFoxConfidenceLevel");
        builder.Property(i => i.CreatedAt).HasColumnName("createdAt");
        builder.Property(i => i.UpdatedAt).HasColumnName("updatedAt");

        builder.HasIndex(i => new { i.ProviderId, i.IocType, i.IocValueHash, i.Port })
            .IsUnique()
            .HasFilter(null);
        builder.HasIndex(i => new { i.ProviderId, i.ProviderRecordId }).IsUnique();
        builder.HasIndex(i => new { i.IocType, i.IocValueHash });
        builder.HasIndex(i => new { i.IocType, i.UrlHost });
        builder.HasIndex(i => new { i.IocType, i.ParentDomain });
        builder.HasIndex(i => i.LastSeenUtc);
    }
}
