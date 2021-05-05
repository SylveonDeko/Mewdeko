using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace NadekoBot.Migrations
{
    public partial class feeds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedSub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GuildConfigId = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedSub", x => x.Id);
                    table.UniqueConstraint("AK_FeedSub_GuildConfigId_Url", x => new { x.GuildConfigId, x.Url });
                    table.ForeignKey(
                        name: "FK_FeedSub_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedSub");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLevelUp",
                table: "UserXpStats",
                nullable: false,
                defaultValue: new DateTime(2017, 9, 15, 5, 48, 8, 665, DateTimeKind.Local),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLevelUp",
                table: "DiscordUser",
                nullable: false,
                defaultValue: new DateTime(2017, 9, 15, 5, 48, 8, 660, DateTimeKind.Local),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 305, DateTimeKind.Local));
        }
    }
}
