using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSettingsAndReportPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aiConnectionSettings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    baseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    modelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    enabled = table.Column<bool>(type: "bit", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aiConnectionSettings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "aiReportPolicyVersions",
                columns: table => new
                {
                    version = table.Column<int>(type: "int", nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    authorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    publishedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aiReportPolicyVersions", x => x.version);
                });

            migrationBuilder.CreateTable(
                name: "aiReportPolicyActive",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    activeVersion = table.Column<int>(type: "int", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aiReportPolicyActive", x => x.id);
                    table.ForeignKey(
                        name: "FK_aiReportPolicyActive_aiReportPolicyVersions_activeVersion",
                        column: x => x.activeVersion,
                        principalTable: "aiReportPolicyVersions",
                        principalColumn: "version",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aiReportPolicyActive_activeVersion",
                table: "aiReportPolicyActive",
                column: "activeVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aiConnectionSettings");

            migrationBuilder.DropTable(
                name: "aiReportPolicyActive");

            migrationBuilder.DropTable(
                name: "aiReportPolicyVersions");
        }
    }
}
