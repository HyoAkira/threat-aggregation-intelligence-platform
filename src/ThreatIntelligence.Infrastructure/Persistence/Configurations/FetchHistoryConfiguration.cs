using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Configurations;

public sealed class FetchHistoryConfiguration : IEntityTypeConfiguration<FetchHistory>
{
    public void Configure(EntityTypeBuilder<FetchHistory> builder)
    {
        builder.ToTable("fetchHistory");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.JobId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(f => f.ProviderId).IsRequired();

        builder.Property(f => f.Trigger)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(f => f.Mode)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(f => f.StartedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(f => f.FinishedAt).HasColumnType("datetime2");

        builder.Property(f => f.Status)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(f => f.RecordsAdded).IsRequired();
        builder.Property(f => f.RecordsUpdated).IsRequired();

        builder.Property(f => f.ErrorDetail).HasColumnType("nvarchar(max)");

        builder.HasIndex(f => f.JobId)
            .IsUnique()
            .HasDatabaseName("IX_fetchHistory_JobId");

        builder.HasIndex(f => new { f.ProviderId, f.StartedAt })
            .HasDatabaseName("IX_fetchHistory_ProviderId_StartedAt");

        builder.HasIndex(f => new { f.Status, f.FinishedAt })
            .HasDatabaseName("IX_fetchHistory_Status_FinishedAt")
            .IncludeProperties(f => new { f.RecordsAdded, f.RecordsUpdated });

        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(f => f.ProviderId);
    }
}