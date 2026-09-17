using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreatIntelligence.Infrastructure.Migrations;

public partial class AddEncryptedSecretStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "requiresApiKey",
            table: "providers",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE providers SET requiresApiKey = 1 WHERE id = 1;");
        migrationBuilder.Sql("UPDATE providers SET requiresApiKey = 0 WHERE id = 2;");

        migrationBuilder.CreateTable(
            name: "encryptedSecrets",
            columns: table => new
            {
                id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                formatVersion = table.Column<int>(type: "int", nullable: false),
                keyVersion = table.Column<int>(type: "int", nullable: false),
                ciphertext = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                nonce = table.Column<byte[]>(type: "binary(12)", nullable: false),
                authenticationTag = table.Column<byte[]>(type: "binary(16)", nullable: false),
                createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                updatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_encryptedSecrets", x => x.id));

        migrationBuilder.CreateIndex(
            name: "IX_encryptedSecrets_Name",
            table: "encryptedSecrets",
            column: "name",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "encryptedSecrets");
        migrationBuilder.DropColumn(name: "requiresApiKey", table: "providers");
    }
}
