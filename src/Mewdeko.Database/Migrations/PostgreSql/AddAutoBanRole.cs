using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

public partial class AddAutoBanRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutoBanRoles",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                RoleId = table.Column<ulong>(nullable: false),
                GuildId = table.Column<ulong>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoBanRoles", x => x.Id);
            });
    }
}