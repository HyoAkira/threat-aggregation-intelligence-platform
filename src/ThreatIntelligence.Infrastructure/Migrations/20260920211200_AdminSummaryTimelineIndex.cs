using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminSummaryTimelineIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_fetchHistory_Status_FinishedAt",
                table: "fetchHistory",
                columns: new[] { "status", "finishedAt" })
                .Annotation("SqlServer:Include", new[] { "recordsAdded", "recordsUpdated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fetchHistory_Status_FinishedAt",
                table: "fetchHistory");
        }
    }
}
