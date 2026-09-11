using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class IoCConfiguration : IEntityTypeConfiguration<IoC>
{
    public void Configure(EntityTypeBuilder<IoC> builder)
    {
        builder.ToTable("iocs", table =>
        {
            table.HasCheckConstraint("CK_iocs_Port",
                "[Port] IS NULL OR ([Port] >= 1 AND [Port] <= 65535)");
            table.HasCheckConstraint("CK_iocs_ThreatFoxConfidenceLevel",
                "[ThreatFoxConfidenceLevel] IS NULL OR ([ThreatFoxConfidenceLevel] >= 0 AND [ThreatFoxConfidenceLevel] <= 100)");
            table.HasCheckConstraint("CK_iocs_PhishTankDetails",
                "[PhishTankDetails] IS NULL OR ISJSON([PhishTankDetails]) = 1");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProviderId).IsRequired();

        builder.Property(i => i.ProviderRecordId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.ProviderDetailsLink).HasMaxLength(2048);

        builder.Property(i => i.IocValue)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(i => i.IocValueHash)
            .HasColumnType("binary(32)")
            .IsRequired();

        builder.Property(i => i.IocType)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(i => i.Port);

        builder.Property(i => i.ThreatType).HasMaxLength(100);
        builder.Property(i => i.Malware).HasMaxLength(200);
        builder.Property(i => i.MalwareAlias).HasMaxLength(500);
        builder.Property(i => i.MalwarePrintable).HasMaxLength(200);

        builder.Property(i => i.FirstSeenUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(i => i.LastSeenUtc).HasColumnType("datetime2");
        builder.Property(i => i.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnType("datetime2").IsRequired();

        builder.Property(i => i.Reporter).HasMaxLength(200);
        builder.Property(i => i.Target).HasMaxLength(500);

        builder.Property(i => i.PhishTankDetails).HasColumnType("nvarchar(max)");

        builder.Property(i => i.UrlHost).HasMaxLength(253);
        builder.Property(i => i.ParentDomain).HasMaxLength(253);

        builder.Property(i => i.ThreatFoxConfidenceLevel)
            .HasConversion<byte?>();

        builder.Property<int>("PortKey")
            .HasColumnName("portKey")
            .HasComputedColumnSql("COALESCE([Port], 0)", stored: true);

        builder.HasIndex("ProviderId", "IocType", "IocValueHash", "PortKey")
            .IsUnique()
            .HasDatabaseName("IX_iocs_EndpointIdentity");

        builder.HasIndex(i => new { i.ProviderId, i.ProviderRecordId })
            .IsUnique()
            .HasDatabaseName("IX_iocs_ProviderRecordId");

        builder.HasIndex(i => new { i.IocType, i.IocValueHash })
            .HasDatabaseName("IX_iocs_IocType_IocValueHash");

        builder.HasIndex(i => new { i.IocType, i.UrlHost })
            .HasDatabaseName("IX_iocs_IocType_UrlHost");

        builder.HasIndex(i => new { i.IocType, i.ParentDomain })
            .HasDatabaseName("IX_iocs_IocType_ParentDomain");

        builder.HasIndex(i => i.LastSeenUtc)
            .HasDatabaseName("IX_iocs_LastSeenUtc");

        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(i => i.ProviderId);
    }
}