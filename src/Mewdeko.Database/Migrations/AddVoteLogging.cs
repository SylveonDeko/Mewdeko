using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddVoteLogging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Votes",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                UserId = table.Column<ulong>(nullable: true),
                GuildId = table.Column<ulong>(nullable: true),
                BotId = table.Column<ulong>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Votes", x => x.Id));

        migrationBuilder.CreateIndex(
            "IX_Votes_GuildId",
            "Votes",
            "Id",
            unique: false);

        migrationBuilder.AddColumn<string>("VotesPassword", "GuildConfigs", defaultValue: "", nullable: true);
        migrationBuilder.AddColumn<ulong>("VotesChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<string>("VoteEmbed", "GuildConfigs", defaultValue: "", nullable: true);

        migrationBuilder.CreateTable(
            "VoteRoles",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                RoleId = table.Column<ulong>(nullable: true),
                GuildId = table.Column<ulong>(nullable: true),
                Timer = table.Column<int>(defaultValue: 0)
            },
            constraints: table => table.PrimaryKey("PK_VoteRoles", x => x.Id));


        migrationBuilder.CreateIndex(
            "IX_VoteRoles_GuildId",
            "VoteRoles",
            "Id",
            unique: false);
    }
}