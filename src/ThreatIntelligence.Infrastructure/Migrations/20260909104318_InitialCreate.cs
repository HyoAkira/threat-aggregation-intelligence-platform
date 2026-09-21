using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    baseUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    isEnabled = table.Column<bool>(type: "bit", nullable: false),
                    intervalMinutes = table.Column<int>(type: "int", nullable: false),
                    lastFetchStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    lastFetchAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    nextFetchTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
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
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    passwordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    role = table.Column<byte>(type: "tinyint", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fetchHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    jobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    providerId = table.Column<int>(type: "int", nullable: false),
                    trigger = table.Column<byte>(type: "tinyint", nullable: false),
                    mode = table.Column<byte>(type: "tinyint", nullable: false),
                    startedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    finishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<byte>(type: "tinyint", nullable: false),
                    recordsAdded = table.Column<int>(type: "int", nullable: false),
                    recordsUpdated = table.Column<int>(type: "int", nullable: false),
                    errorDetail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fetchHistory", x => x.id);
                    table.ForeignKey(
                        name: "FK_fetchHistory_providers_providerId",
                        column: x => x.providerId,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    portKey = table.Column<int>(type: "int", nullable: false, computedColumnSql: "COALESCE([Port], 0)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iocs", x => x.id);
                    table.CheckConstraint("CK_iocs_PhishTankDetails", "[PhishTankDetails] IS NULL OR ISJSON([PhishTankDetails]) = 1");
                    table.CheckConstraint("CK_iocs_Port", "[Port] IS NULL OR ([Port] >= 1 AND [Port] <= 65535)");
                    table.CheckConstraint("CK_iocs_ThreatFoxConfidenceLevel", "[ThreatFoxConfidenceLevel] IS NULL OR ([ThreatFoxConfidenceLevel] >= 0 AND [ThreatFoxConfidenceLevel] <= 100)");
                    table.ForeignKey(
                        name: "FK_iocs_providers_providerId",
                        column: x => x.providerId,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fetchHistory_JobId",
                table: "fetchHistory",
                column: "jobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fetchHistory_ProviderId_StartedAt",
                table: "fetchHistory",
                columns: new[] { "providerId", "startedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_EndpointIdentity",
                table: "iocs",
                columns: new[] { "providerId", "iocType", "iocValueHash", "portKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iocs_IocType_IocValueHash",
                table: "iocs",
                columns: new[] { "iocType", "iocValueHash" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_IocType_ParentDomain",
                table: "iocs",
                columns: new[] { "iocType", "parentDomain" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_IocType_UrlHost",
                table: "iocs",
                columns: new[] { "iocType", "urlHost" });

            migrationBuilder.CreateIndex(
                name: "IX_iocs_LastSeenUtc",
                table: "iocs",
                column: "lastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_iocs_ProviderRecordId",
                table: "iocs",
                columns: new[] { "providerId", "providerRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iocTags_tagId",
                table: "iocTags",
                column: "tagId");

            migrationBuilder.CreateIndex(
                name: "IX_settings_Key",
                table: "settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_Name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fetchHistory");

            migrationBuilder.DropTable(
                name: "iocTags");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "iocs");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "providers");
        }
    }
}
