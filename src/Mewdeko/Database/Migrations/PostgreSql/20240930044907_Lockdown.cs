#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class Lockdown : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "LockdownChannelPermissions",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: false),
                AllowPermissions = table.Column<decimal>("numeric(20,0)", nullable: false),
                DenyPermissions = table.Column<decimal>("numeric(20,0)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LockdownChannelPermissions", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "LockdownChannelPermissions");
    }
}