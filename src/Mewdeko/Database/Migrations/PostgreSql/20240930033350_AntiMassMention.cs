#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class AntiMassMention : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            "AntiMassMentionSettingId",
            "AntiSpamIgnore",
            "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            "AntiMassMentionSetting",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildConfigId = table.Column<int>("integer", nullable: false),
                Action = table.Column<int>("integer", nullable: false),
                MentionThreshold = table.Column<int>("integer", nullable: false),
                MaxMentionsInTimeWindow = table.Column<int>("integer", nullable: false),
                TimeWindowSeconds = table.Column<int>("integer", nullable: false),
                MuteTime = table.Column<int>("integer", nullable: false),
                RoleId = table.Column<decimal>("numeric(20,0)", nullable: true),
                IgnoreBots = table.Column<bool>("boolean", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AntiMassMentionSetting", x => x.Id);
                table.ForeignKey(
                    "FK_AntiMassMentionSetting_GuildConfigs_GuildConfigId",
                    x => x.GuildConfigId,
                    "GuildConfigs",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_AntiSpamIgnore_AntiMassMentionSettingId",
            "AntiSpamIgnore",
            "AntiMassMentionSettingId");

        migrationBuilder.CreateIndex(
            "IX_AntiMassMentionSetting_GuildConfigId",
            "AntiMassMentionSetting",
            "GuildConfigId",
            unique: true);

        migrationBuilder.AddForeignKey(
            "FK_AntiSpamIgnore_AntiMassMentionSetting_AntiMassMentionSettin~",
            "AntiSpamIgnore",
            "AntiMassMentionSettingId",
            "AntiMassMentionSetting",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            "FK_AntiSpamIgnore_AntiMassMentionSetting_AntiMassMentionSettin~",
            "AntiSpamIgnore");

        migrationBuilder.DropTable(
            "AntiMassMentionSetting");

        migrationBuilder.DropIndex(
            "IX_AntiSpamIgnore_AntiMassMentionSettingId",
            "AntiSpamIgnore");

        migrationBuilder.DropColumn(
            "AntiMassMentionSettingId",
            "AntiSpamIgnore");
    }
}