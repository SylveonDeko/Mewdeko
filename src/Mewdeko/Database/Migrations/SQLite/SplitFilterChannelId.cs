using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

/// <inheritdoc />
public partial class SplitFilterChannelId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "FilterInvitesChannelIds",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChannelId = table.Column<ulong>("INTEGER", nullable: false),
                GuildConfigId = table.Column<int>("INTEGER", nullable: false),
                DateAdded = table.Column<DateTime>("TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterInvitesChannelIds", x => x.Id);
                table.ForeignKey(
                    "FK_FilterInvitesChannelIds_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            "FilterWordsChannelIds",
            table => new
            {
                Id = table.Column<int>("INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ChannelId = table.Column<ulong>("INTEGER", nullable: false),
                GuildConfigId = table.Column<int>("INTEGER", nullable: false),
                DateAdded = table.Column<DateTime>("TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterWordsChannelIds", x => x.Id);
                table.ForeignKey(
                    "FK_FilterWordsChannelIds_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
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
            "FilterChannelId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recreate the old table
        migrationBuilder.CreateTable(
            "FilterChannelId",
            table => new
            {
                ChannelId = table.Column<ulong>("INTEGER", nullable: false),
                GuildConfigId = table.Column<int>("INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterChannelId", x => new
                {
                    x.GuildConfigId, x.ChannelId
                });
                table.ForeignKey(
                    "FK_FilterChannelId_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
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
            "FilterInvitesChannelIds");

        migrationBuilder.DropTable(
            "FilterWordsChannelIds");
    }
}