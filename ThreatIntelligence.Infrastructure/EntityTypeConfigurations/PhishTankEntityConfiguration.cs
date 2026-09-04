using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ThreatIntelligence.Infrastructure.EntityTypeConfigurations;

public class PhishTankEntityConfiguration : IEntityTypeConfiguration<PhishTank>
{
    public void Configure(EntityTypeBuilder<PhishTank> builder)
    {
        builder.ToTable("PhishTank", "providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PhishId).IsRequired();
        builder.Property(p => p.Url).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(p => p.PhishDetailUrl).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(p => p.SubmissionTime).IsRequired();
        builder.Property(p => p.Verified).IsRequired();
        builder.Property(p => p.VerificationTime);
        builder.Property(p => p.Online).IsRequired();
        builder.Property(p => p.Target).HasMaxLength(500);
        builder.Property(p => p.Details).HasColumnType("nvarchar(max)");

        builder.HasIndex(p => p.PhishId).IsUnique();
    }
}
