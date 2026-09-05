using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Services.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "providers");

            migrationBuilder.EnsureSchema(
                name: "threat");

            migrationBuilder.CreateTable(
                name: "PhishTank",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhishId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhishDetailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Verified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Online = table.Column<bool>(type: "bit", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhishTank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provider",
                schema: "threat",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FetchIntervalMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provider", x => x.ProviderId);
                });

            migrationBuilder.CreateTable(
                name: "ThreatFox",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IocId = table.Column<int>(type: "int", nullable: false),
                    IocValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IocType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ThreatType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Malware = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MalwareAlias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MalwarePrintable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfidenceLevel = table.Column<int>(type: "int", nullable: false),
                    IsCompromised = table.Column<bool>(type: "bit", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Anonymous = table.Column<int>(type: "int", nullable: false),
                    Reporter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreatFox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhishTank_PhishId",
                schema: "providers",
                table: "PhishTank",
                column: "PhishId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreatFox_IocId_IocValue_IocType",
                schema: "providers",
                table: "ThreatFox",
                columns: new[] { "IocId", "IocValue", "IocType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhishTank",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "Provider",
                schema: "threat");

            migrationBuilder.DropTable(
                name: "ThreatFox",
                schema: "providers");
        }
    }
}
