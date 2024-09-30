using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AntiMassMention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AntiMassMentionSettingId",
                table: "AntiSpamIgnore",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AntiMassMentionSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    MentionThreshold = table.Column<int>(type: "integer", nullable: false),
                    MaxMentionsInTimeWindow = table.Column<int>(type: "integer", nullable: false),
                    TimeWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    MuteTime = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    IgnoreBots = table.Column<bool>(type: "boolean", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiMassMentionSetting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntiMassMentionSetting_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntiSpamIgnore_AntiMassMentionSettingId",
                table: "AntiSpamIgnore",
                column: "AntiMassMentionSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_AntiMassMentionSetting_GuildConfigId",
                table: "AntiMassMentionSetting",
                column: "GuildConfigId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AntiSpamIgnore_AntiMassMentionSetting_AntiMassMentionSettin~",
                table: "AntiSpamIgnore",
                column: "AntiMassMentionSettingId",
                principalTable: "AntiMassMentionSetting",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AntiSpamIgnore_AntiMassMentionSetting_AntiMassMentionSettin~",
                table: "AntiSpamIgnore");

            migrationBuilder.DropTable(
                name: "AntiMassMentionSetting");

            migrationBuilder.DropIndex(
                name: "IX_AntiSpamIgnore_AntiMassMentionSettingId",
                table: "AntiSpamIgnore");

            migrationBuilder.DropColumn(
                name: "AntiMassMentionSettingId",
                table: "AntiSpamIgnore");
        }
    }
}
