using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddAutoPublish : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublishUserBlacklist",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                ChannelId = table.Column<ulong>(nullable: false),
                User = table.Column<ulong>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublishUserBlacklist", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PublishWordBlacklist",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                ChannelId = table.Column<ulong>(nullable: false),
                Word = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublishWordBlacklist", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AutoPublish",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                GuildId = table.Column<ulong>(nullable: false),
                ChannelId = table.Column<ulong>(nullable: false),
                BlacklistedUsers = table.Column<ulong>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoPublish", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PublishUserBlacklist");
        migrationBuilder.DropTable("PublishWordBlacklist");
        migrationBuilder.DropTable("AutoPublish");
    }
}