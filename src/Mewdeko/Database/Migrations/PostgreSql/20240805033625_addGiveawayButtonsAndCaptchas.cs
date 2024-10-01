#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class addGiveawayButtonsAndCaptchas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_PollAnswer_Poll_PollId",
            "PollAnswer");

        migrationBuilder.DropForeignKey(
            "FK_PollVote_Poll_PollId",
            "PollVote");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateBar_TemplateBarId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateClub_TemplateClubId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateGuild_TemplateGuildId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateUser_TemplateUserId",
            "Template");

        migrationBuilder.DropIndex(
            "IX_PollVote_PollId",
            "PollVote");

        migrationBuilder.RenameColumn(
            "PollId",
            "PollAnswer",
            "PollsId");

        migrationBuilder.RenameIndex(
            "IX_PollAnswer_PollId",
            table: "PollAnswer",
            newName: "IX_PollAnswer_PollsId");

        migrationBuilder.AlterColumn<int>(
            "TemplateUserId",
            "Template",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            "TemplateGuildId",
            "Template",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            "TemplateClubId",
            "Template",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            "TemplateBarId",
            "Template",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            "PollId",
            "PollVote",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddColumn<int>(
            "PollsId",
            "PollVote",
            "integer",
            nullable: true);

        migrationBuilder.AlterColumn<int>(
            "NextId",
            "Permission",
            "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Message",
            "MultiGreets",
            "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AddColumn<bool>(
            "UseButton",
            "Giveaways",
            "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            "UseCaptcha",
            "Giveaways",
            "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AlterColumn<string>(
            "NameOrId",
            "CommandStats",
            "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.CreateTable(
            "GiveawayUsers",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GiveawayId = table.Column<int>("integer", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GiveawayUsers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            "IX_PollVote_PollsId",
            "PollVote",
            "PollsId");

        migrationBuilder.AddForeignKey(
            "FK_PollAnswer_Poll_PollsId",
            "PollAnswer",
            "PollsId",
            "Poll",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_PollVote_Poll_PollsId",
            "PollVote",
            "PollsId",
            "Poll",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateBar_TemplateBarId",
            "Template",
            "TemplateBarId",
            "TemplateBar",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateClub_TemplateClubId",
            "Template",
            "TemplateClubId",
            "TemplateClub",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateGuild_TemplateGuildId",
            "Template",
            "TemplateGuildId",
            "TemplateGuild",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateUser_TemplateUserId",
            "Template",
            "TemplateUserId",
            "TemplateUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_PollAnswer_Poll_PollsId",
            "PollAnswer");

        migrationBuilder.DropForeignKey(
            "FK_PollVote_Poll_PollsId",
            "PollVote");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateBar_TemplateBarId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateClub_TemplateClubId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateGuild_TemplateGuildId",
            "Template");

        migrationBuilder.DropForeignKey(
            "FK_Template_TemplateUser_TemplateUserId",
            "Template");

        migrationBuilder.DropTable(
            "GiveawayUsers");

        migrationBuilder.DropIndex(
            "IX_PollVote_PollsId",
            "PollVote");

        migrationBuilder.DropColumn(
            "PollsId",
            "PollVote");

        migrationBuilder.DropColumn(
            "UseButton",
            "Giveaways");

        migrationBuilder.DropColumn(
            "UseCaptcha",
            "Giveaways");

        migrationBuilder.RenameColumn(
            "PollsId",
            "PollAnswer",
            "PollId");

        migrationBuilder.RenameIndex(
            "IX_PollAnswer_PollsId",
            table: "PollAnswer",
            newName: "IX_PollAnswer_PollId");

        migrationBuilder.AlterColumn<int>(
            "TemplateUserId",
            "Template",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            "TemplateGuildId",
            "Template",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            "TemplateClubId",
            "Template",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            "TemplateBarId",
            "Template",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            "PollId",
            "PollVote",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            "NextId",
            "Permission",
            "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<string>(
            "Message",
            "MultiGreets",
            "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "NameOrId",
            "CommandStats",
            "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            "IX_PollVote_PollId",
            "PollVote",
            "PollId");

        migrationBuilder.AddForeignKey(
            "FK_PollAnswer_Poll_PollId",
            "PollAnswer",
            "PollId",
            "Poll",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_PollVote_Poll_PollId",
            "PollVote",
            "PollId",
            "Poll",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateBar_TemplateBarId",
            "Template",
            "TemplateBarId",
            "TemplateBar",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateClub_TemplateClubId",
            "Template",
            "TemplateClubId",
            "TemplateClub",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateGuild_TemplateGuildId",
            "Template",
            "TemplateGuildId",
            "TemplateGuild",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            "FK_Template_TemplateUser_TemplateUserId",
            "Template",
            "TemplateUserId",
            "TemplateUser",
            principalColumn: "Id");
    }
}