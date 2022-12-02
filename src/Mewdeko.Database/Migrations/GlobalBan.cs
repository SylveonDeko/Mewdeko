using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class GlobalBan : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "GlobalBan",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                UserId = table.Column<ulong>(nullable: true),
                Proof = table.Column<string>(nullable: true),
                Reason = table.Column<string>(nullable: true),
                AddedBy = table.Column<ulong>(nullable: true),
                Type = table.Column<string>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_GlobalBan", x => x.Id));
        migrationBuilder.AddColumn<int>("GBAction", "GuildConfigs", defaultValue: 1);
        migrationBuilder.AddColumn<int>("GBEnabled", "GuildConfigs", defaultValue: 1);

        migrationBuilder.CreateIndex(
            "IX_GlobalBan_GuildId",
            "GlobalBan",
            "UserId",
            unique: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(
            "GlobalBan");
}