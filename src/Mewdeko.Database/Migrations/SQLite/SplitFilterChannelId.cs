using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class SplitFilterChannelId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FilterInvitesChannelIds",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                GuildConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterInvitesChannelIds", x => x.Id);
                table.ForeignKey(
                    name: "FK_FilterInvitesChannelIds_GuildConfigs_GuildConfigId",
                    column: x => x.GuildConfigId,
                    principalTable: "GuildConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FilterWordsChannelIds",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                GuildConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterWordsChannelIds", x => x.Id);
                table.ForeignKey(
                    name: "FK_FilterWordsChannelIds_GuildConfigs_GuildConfigId",
                    column: x => x.GuildConfigId,
                    principalTable: "GuildConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            "INSERT INTO FilterWordsChannelIds (GuildConfigId, ChannelId, DateAdded) SELECT GuildConfigId1, ChannelId, DateAdded FROM FilterChannelId WHERE GuildConfigId1 IS NOT NULL"
        );
        migrationBuilder.Sql(
            "INSERT INTO FilterInvitesChannelIds (GuildConfigId, ChannelId, DateAdded) SELECT GuildConfigId, ChannelId, DateAdded FROM FilterChannelId WHERE GuildConfigId IS NOT NULL"
        );


        // Drop the old table
        migrationBuilder.DropTable(
            name: "FilterChannelId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recreate the old table
        migrationBuilder.CreateTable(
            name: "FilterChannelId",
            columns: table => new
            {
                ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false), GuildConfigId = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterChannelId", x => new
                {
                    x.GuildConfigId, x.ChannelId
                });
                table.ForeignKey(
                    name: "FK_FilterChannelId_GuildConfigs_GuildConfigId",
                    column: x => x.GuildConfigId,
                    principalTable: "GuildConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Migrate data from new tables to old table
        // Migrate data from new tables to old table
        migrationBuilder.Sql(
            "INSERT INTO FilterChannelId (Id, GuildConfigId1, ChannelId, DateAdded) SELECT Id, GuildConfigId, ChannelId, DateAdded FROM FilterWordsChannelIds"
        );
        migrationBuilder.Sql(
            "INSERT INTO FilterChannelId (Id, GuildConfigId, ChannelId, DateAdded) SELECT Id, GuildConfigId, ChannelId, DateAdded FROM FilterInvitesChannelIds"
        );


        // Drop the new tables
        migrationBuilder.DropTable(
            name: "FilterInvitesChannelIds");

        migrationBuilder.DropTable(
            name: "FilterWordsChannelIds");
    }
}