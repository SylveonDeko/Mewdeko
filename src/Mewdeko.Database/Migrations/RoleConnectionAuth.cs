using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class RoleConnectionAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("AuthCodes", columns => new
        {
            Id = columns.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            DateAdded = columns.Column<DateTime>(),
            UserId = columns.Column<ulong>(nullable: false),
            Scopes = columns.Column<string>(nullable: true, defaultValue: "identify"),
            Token = columns.Column<string>(),
            RefreshToken = columns.Column<string>(),
            ExpiresAt = columns.Column<DateTime>()
        }, constraints: table => table.PrimaryKey("PK_AuthCodes", x => x.Id));
    }
}