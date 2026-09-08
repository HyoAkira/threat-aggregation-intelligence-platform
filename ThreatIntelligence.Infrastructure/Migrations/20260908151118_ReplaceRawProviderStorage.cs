using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ThreatIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceRawProviderStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhishTank",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "ThreatFox",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "Provider",
                schema: "threat");

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    baseUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    isEnabled = table.Column<bool>(type: "bit", nullable: false),
                    intervalMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iocs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    providerId = table.Column<int>(type: "int", nullable: false),
                    providerRecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    providerDetailsLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    iocValue = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    iocValueHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    iocType = table.Column<byte>(type: "tinyint", nullable: false),
                    port = table.Column<int>(type: "int", nullable: true),
                    threatType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    malware = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    malwareAlias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    malwarePrintable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    firstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    lastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    isCompromised = table.Column<bool>(type: "bit", nullable: true),
                    reporter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    anonymous = table.Column<bool>(type: "bit", nullable: true),
                    isVerified = table.Column<bool>(type: "bit", nullable: true),
                    target = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    phishTankDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phishTankOnline = table.Column<bool>(type: "bit", nullable: true),
                    urlHost = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    parentDomain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    threatFoxConfidenceLevel = table.Column<byte>(type: "tinyint", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iocs", x => x.id);
                    table.CheckConstraint("CK_iocs_phishTankDetails", "[phishTankDetails] IS NULL OR ISJSON([phishTankDetails]) = 1");
                    table.CheckConstraint("CK_iocs_port", "[port] IS NULL OR [port] BETWEEN 1 AND 65535");
                    table.CheckConstraint("CK_iocs_threatFoxConfidenceLevel", "[threatFoxConfidenceLevel] IS NULL OR [threatFoxConfidenceLevel] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_iocs_providers_providerId",
                        column: x => x.providerId,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iocTags",
                columns: table => new
                {
                    iocId = table.Column<int>(type: "int", nullable: false),
                    tagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iocTags", x => new { x.iocId, x.tagId });
                    table.ForeignKey(
                        name: "FK_iocTags_iocs_iocId",
                        column: x => x.iocId,
                        principalTable: "iocs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iocTags_tags_tagId",
                        column: x => x.tagId,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "baseUrl", "intervalMinutes", "isEnabled", "name" },
                values: new object[,]
                {
                    { 1, "https://threatfox-api.abuse.ch", 5, true, "ThreatFox" },
                    { 2, "http://data.phishtank.com/data/", 60, true, "PhishTank" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_iocType_iocValueHash",
                table: "iocs",
                columns: new[] { "iocType", "iocValueHash" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_iocType_parentDomain",
                table: "iocs",
                columns: new[] { "iocType", "parentDomain" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_iocType_urlHost",
                table: "iocs",
                columns: new[] { "iocType", "urlHost" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_lastSeenUtc",
                table: "iocs",
                column: "lastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_iocs_providerId_iocType_iocValueHash_port",
                table: "iocs",
                columns: new[] { "providerId", "iocType", "iocValueHash", "port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iocs_providerId_providerRecordId",
                table: "iocs",
                columns: new[] { "providerId", "providerRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iocTags_tagId",
                table: "iocTags",
                column: "tagId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                table: "tags",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iocTags");

            migrationBuilder.DropTable(
                name: "iocs");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.EnsureSchema(
                name: "providers");

            migrationBuilder.EnsureSchema(
                name: "threat");

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
                name: "PhishTank",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Online = table.Column<bool>(type: "bit", nullable: false),
                    PhishDetailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhishId = table.Column<int>(type: "int", nullable: false),
                    SubmissionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Target = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Verified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhishTank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreatFox",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anonymous = table.Column<int>(type: "int", nullable: false),
                    ConfidenceLevel = table.Column<int>(type: "int", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IocId = table.Column<int>(type: "int", nullable: false),
                    IocType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IocValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCompromised = table.Column<bool>(type: "bit", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Malware = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MalwareAlias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MalwarePrintable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reporter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThreatType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
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
    }
}
