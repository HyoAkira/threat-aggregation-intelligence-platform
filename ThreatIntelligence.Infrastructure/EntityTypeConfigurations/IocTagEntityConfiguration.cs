using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.EntityTypeConfigurations;

public class IocTagEntityConfiguration : IEntityTypeConfiguration<IocTag>
{
    public void Configure(EntityTypeBuilder<IocTag> builder)
    {
        builder.ToTable("iocTags");

        builder.HasKey(it => new { it.IocId, it.TagId });
        builder.Property(it => it.IocId).HasColumnName("iocId");
        builder.Property(it => it.TagId).HasColumnName("tagId");

        builder.HasOne(it => it.Ioc)
            .WithMany(i => i.IocTags)
            .HasForeignKey(it => it.IocId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(it => it.Tag)
            .WithMany(t => t.IocTags)
            .HasForeignKey(it => it.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
