using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class IoCTagConfiguration : IEntityTypeConfiguration<IoCTag>
{
    public void Configure(EntityTypeBuilder<IoCTag> builder)
    {
        builder.ToTable("iocTags");

        builder.HasKey(t => new { t.IocId, t.TagId });

        builder.Ignore(t => t.Id);

        builder.Property(t => t.IocId).IsRequired();
        builder.Property(t => t.TagId).IsRequired();

        builder.HasOne<IoC>()
            .WithMany()
            .HasForeignKey(t => t.IocId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}