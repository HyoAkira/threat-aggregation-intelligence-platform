using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class AiReportPolicyActiveConfiguration : IEntityTypeConfiguration<AiReportPolicyActive>
{
    public void Configure(EntityTypeBuilder<AiReportPolicyActive> builder)
    {
        builder.ToTable("aiReportPolicyActive");
        builder.HasKey(pointer => pointer.Id);
        builder.Property(pointer => pointer.Id).ValueGeneratedNever();
        builder.Property(pointer => pointer.ActiveVersion).IsRequired();
        builder.Property(pointer => pointer.UpdatedAt)
            .HasColumnType("datetime2")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(pointer => pointer.Version).IsRowVersion();

        builder.HasOne<AiReportPolicyVersion>()
            .WithMany()
            .HasForeignKey(pointer => pointer.ActiveVersion);
    }
}
