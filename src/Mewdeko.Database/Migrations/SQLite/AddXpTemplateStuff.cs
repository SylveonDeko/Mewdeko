using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class AddTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TemplateUser",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                TextColor = table.Column<string>(nullable: true),
                FontSize = table.Column<int>(nullable: false),
                TextX = table.Column<int>(nullable: false),
                TextY = table.Column<int>(nullable: false),
                ShowText = table.Column<bool>(nullable: false),
                IconX = table.Column<int>(nullable: false),
                IconY = table.Column<int>(nullable: false),
                IconSizeX = table.Column<int>(nullable: false),
                IconSizeY = table.Column<int>(nullable: false),
                ShowIcon = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateUser", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TemplateGuild",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                GuildLevelColor = table.Column<string>(nullable: true),
                GuildLevelFontSize = table.Column<int>(nullable: false),
                GuildLevelX = table.Column<int>(nullable: false),
                GuildLevelY = table.Column<int>(nullable: false),
                ShowGuildLevel = table.Column<bool>(nullable: false),
                GuildRankColor = table.Column<string>(nullable: true),
                GuildRankFontSize = table.Column<int>(nullable: false),
                GuildRankX = table.Column<int>(nullable: false),
                GuildRankY = table.Column<int>(nullable: false),
                ShowGuildRank = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateGuild", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TemplateClub",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                ClubIconX = table.Column<int>(nullable: false),
                ClubIconY = table.Column<int>(nullable: false),
                ClubIconSizeX = table.Column<int>(nullable: false),
                ClubIconSizeY = table.Column<int>(nullable: false),
                ShowClubIcon = table.Column<bool>(nullable: false),
                ClubNameColor = table.Column<string>(nullable: true),
                ClubNameFontSize = table.Column<int>(nullable: false),
                ClubNameX = table.Column<int>(nullable: false),
                ClubNameY = table.Column<int>(nullable: false),
                ShowClubName = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateClub", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TemplateBar",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                BarColor = table.Column<string>(nullable: true),
                BarPointAx = table.Column<int>(nullable: false),
                BarPointAy = table.Column<int>(nullable: false),
                BarPointBx = table.Column<int>(nullable: false),
                BarPointBy = table.Column<int>(nullable: false),
                BarLength = table.Column<int>(nullable: false),
                BarTransparency = table.Column<byte>(nullable: false),
                BarDirection = table.Column<int>(nullable: false),
                ShowBar = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateBar", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Template",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                GuildId = table.Column<long>(nullable: false),
                OutputSizeX = table.Column<int>(nullable: false),
                OutputSizeY = table.Column<int>(nullable: false),
                TimeOnLevelFormat = table.Column<string>(nullable: true),
                TimeOnLevelX = table.Column<int>(nullable: false),
                TimeOnLevelY = table.Column<int>(nullable: false),
                TimeOnLevelFontSize = table.Column<int>(nullable: false),
                TimeOnLevelColor = table.Column<string>(nullable: true),
                ShowTimeOnLevel = table.Column<bool>(nullable: false),
                AwardedX = table.Column<int>(nullable: false),
                AwardedY = table.Column<int>(nullable: false),
                AwardedFontSize = table.Column<int>(nullable: false),
                AwardedColor = table.Column<string>(nullable: true),
                ShowAwarded = table.Column<bool>(nullable: false),
                TemplateUserId = table.Column<int>(nullable: true),
                TemplateGuildId = table.Column<int>(nullable: true),
                TemplateClubId = table.Column<int>(nullable: true),
                TemplateBarId = table.Column<int>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Template", x => x.Id);
                table.ForeignKey(
                    name: "FK_Template_TemplateUser_TemplateUserId",
                    column: x => x.TemplateUserId,
                    principalTable: "TemplateUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Template_TemplateGuild_TemplateGuildId",
                    column: x => x.TemplateGuildId,
                    principalTable: "TemplateGuild",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Template_TemplateClub_TemplateClubId",
                    column: x => x.TemplateClubId,
                    principalTable: "TemplateClub",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Template_TemplateBar_TemplateBarId",
                    column: x => x.TemplateBarId,
                    principalTable: "TemplateBar",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Template");

        migrationBuilder.DropTable(
            name: "TemplateUser");

        migrationBuilder.DropTable(
            name: "TemplateGuild");

        migrationBuilder.DropTable(
            name: "TemplateClub");

        migrationBuilder.DropTable(
            name: "TemplateBar");
    }
}