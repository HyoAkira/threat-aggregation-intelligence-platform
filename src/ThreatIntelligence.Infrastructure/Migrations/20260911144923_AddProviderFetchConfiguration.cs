using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderFetchConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "defaultMode",
                table: "providers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<int>(
                name: "fullCooldownMinutes",
                table: "providers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "minimumIntervalMinutes",
                table: "providers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "recentCooldownMinutes",
                table: "providers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "supportedModes",
                table: "providers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill the deterministic seeded providers; other rows keep the column defaults.
            migrationBuilder.Sql("""
                UPDATE providers
                SET defaultMode = 0, supportedModes = 3, recentCooldownMinutes = 5,
                    fullCooldownMinutes = 60, minimumIntervalMinutes = 5
                WHERE id = 1;

                UPDATE providers
                SET defaultMode = 1, supportedModes = 2, recentCooldownMinutes = NULL,
                    fullCooldownMinutes = NULL, minimumIntervalMinutes = 480
                WHERE id = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "defaultMode",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "fullCooldownMinutes",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "minimumIntervalMinutes",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "recentCooldownMinutes",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "supportedModes",
                table: "providers");
        }
    }
}
