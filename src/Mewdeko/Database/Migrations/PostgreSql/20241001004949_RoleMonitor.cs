#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class RoleMonitor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "BlacklistedPermissions",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Permission = table.Column<decimal>("numeric(20,0)", nullable: false),
                PunishmentAction = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BlacklistedPermissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "BlacklistedRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                PunishmentAction = table.Column<int>("integer", nullable: true),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BlacklistedRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "RoleMonitoringSettings",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DefaultPunishmentAction = table.Column<int>("integer", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleMonitoringSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "WhitelistedRoles",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhitelistedRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "WhitelistedUsers",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WhitelistedUsers", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "BlacklistedPermissions");

        migrationBuilder.DropTable(
            "BlacklistedRoles");

        migrationBuilder.DropTable(
            "RoleMonitoringSettings");

        migrationBuilder.DropTable(
            "WhitelistedRoles");

        migrationBuilder.DropTable(
            "WhitelistedUsers");
    }
}