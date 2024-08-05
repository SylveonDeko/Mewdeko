using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class addGiveawayButtonsAndCaptchas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollAnswer_Poll_PollId",
                table: "PollAnswer");

            migrationBuilder.DropForeignKey(
                name: "FK_PollVote_Poll_PollId",
                table: "PollVote");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateBar_TemplateBarId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateClub_TemplateClubId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateGuild_TemplateGuildId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateUser_TemplateUserId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_PollVote_PollId",
                table: "PollVote");

            migrationBuilder.RenameColumn(
                name: "PollId",
                table: "PollAnswer",
                newName: "PollsId");

            migrationBuilder.RenameIndex(
                name: "IX_PollAnswer_PollId",
                table: "PollAnswer",
                newName: "IX_PollAnswer_PollsId");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateUserId",
                table: "Template",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemplateGuildId",
                table: "Template",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemplateClubId",
                table: "Template",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemplateBarId",
                table: "Template",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PollId",
                table: "PollVote",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PollsId",
                table: "PollVote",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NextId",
                table: "Permission",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "MultiGreets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "UseButton",
                table: "Giveaways",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseCaptcha",
                table: "Giveaways",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "NameOrId",
                table: "CommandStats",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "GiveawayUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GiveawayId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollVote_PollsId",
                table: "PollVote",
                column: "PollsId");

            migrationBuilder.AddForeignKey(
                name: "FK_PollAnswer_Poll_PollsId",
                table: "PollAnswer",
                column: "PollsId",
                principalTable: "Poll",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PollVote_Poll_PollsId",
                table: "PollVote",
                column: "PollsId",
                principalTable: "Poll",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateBar_TemplateBarId",
                table: "Template",
                column: "TemplateBarId",
                principalTable: "TemplateBar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateClub_TemplateClubId",
                table: "Template",
                column: "TemplateClubId",
                principalTable: "TemplateClub",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateGuild_TemplateGuildId",
                table: "Template",
                column: "TemplateGuildId",
                principalTable: "TemplateGuild",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateUser_TemplateUserId",
                table: "Template",
                column: "TemplateUserId",
                principalTable: "TemplateUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollAnswer_Poll_PollsId",
                table: "PollAnswer");

            migrationBuilder.DropForeignKey(
                name: "FK_PollVote_Poll_PollsId",
                table: "PollVote");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateBar_TemplateBarId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateClub_TemplateClubId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateGuild_TemplateGuildId",
                table: "Template");

            migrationBuilder.DropForeignKey(
                name: "FK_Template_TemplateUser_TemplateUserId",
                table: "Template");

            migrationBuilder.DropTable(
                name: "GiveawayUsers");

            migrationBuilder.DropIndex(
                name: "IX_PollVote_PollsId",
                table: "PollVote");

            migrationBuilder.DropColumn(
                name: "PollsId",
                table: "PollVote");

            migrationBuilder.DropColumn(
                name: "UseButton",
                table: "Giveaways");

            migrationBuilder.DropColumn(
                name: "UseCaptcha",
                table: "Giveaways");

            migrationBuilder.RenameColumn(
                name: "PollsId",
                table: "PollAnswer",
                newName: "PollId");

            migrationBuilder.RenameIndex(
                name: "IX_PollAnswer_PollsId",
                table: "PollAnswer",
                newName: "IX_PollAnswer_PollId");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateUserId",
                table: "Template",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateGuildId",
                table: "Template",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateClubId",
                table: "Template",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateBarId",
                table: "Template",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PollId",
                table: "PollVote",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "NextId",
                table: "Permission",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "MultiGreets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameOrId",
                table: "CommandStats",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollVote_PollId",
                table: "PollVote",
                column: "PollId");

            migrationBuilder.AddForeignKey(
                name: "FK_PollAnswer_Poll_PollId",
                table: "PollAnswer",
                column: "PollId",
                principalTable: "Poll",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PollVote_Poll_PollId",
                table: "PollVote",
                column: "PollId",
                principalTable: "Poll",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateBar_TemplateBarId",
                table: "Template",
                column: "TemplateBarId",
                principalTable: "TemplateBar",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateClub_TemplateClubId",
                table: "Template",
                column: "TemplateClubId",
                principalTable: "TemplateClub",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateGuild_TemplateGuildId",
                table: "Template",
                column: "TemplateGuildId",
                principalTable: "TemplateGuild",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Template_TemplateUser_TemplateUserId",
                table: "Template",
                column: "TemplateUserId",
                principalTable: "TemplateUser",
                principalColumn: "Id");
        }
    }
}
