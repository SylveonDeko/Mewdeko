using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class AddAutoPublish : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "PublishUserBlacklist",
            table => new
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
            "PublishWordBlacklist",
            table => new
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
            "AutoPublish",
            table => new
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PublishUserBlacklist");
        migrationBuilder.DropTable("PublishWordBlacklist");
        migrationBuilder.DropTable("AutoPublish");
    }
}