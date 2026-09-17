using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class AiReportPolicyVersionConfiguration : IEntityTypeConfiguration<AiReportPolicyVersion>
{
    public void Configure(EntityTypeBuilder<AiReportPolicyVersion> builder)
    {
        builder.ToTable("aiReportPolicyVersions");
        builder.HasKey(version => version.Version);
        builder.Property(version => version.Version).ValueGeneratedNever();
        builder.Property(version => version.Text).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(version => version.AuthorUserId).HasMaxLength(450);
        builder.Property(version => version.PublishedAt)
            .HasColumnType("datetime2")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
    }
}
