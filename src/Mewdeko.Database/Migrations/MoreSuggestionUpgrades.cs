using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class MoreSuggestionUpgrades : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("EmoteCount1", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("EmoteCount2", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("EmoteCount3", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("EmoteCount4", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("EmoteCount5", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<int>("EmoteMode", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("AcceptChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("DenyChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ImplementChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ConsiderChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("StateChangeUser", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("StateChangeCount", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<int>("Current State", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("SuggestButtonChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<int>("Emote1Style", "GuildConfigs", defaultValue: 2);
        migrationBuilder.AddColumn<int>("Emote2Style", "GuildConfigs", defaultValue: 2);
        migrationBuilder.AddColumn<int>("Emote3Style", "GuildConfigs", defaultValue: 2);
        migrationBuilder.AddColumn<int>("Emote4Style", "GuildConfigs", defaultValue: 2);
        migrationBuilder.AddColumn<int>("Emote5Style", "GuildConfigs", defaultValue: 2);
        migrationBuilder.CreateTable("SuggestVotes",
            builder => new
            {
                Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                UserId = builder.Column<ulong>(nullable: false),
                MessageId = builder.Column<ulong>(nullable: false),
                EmotePicked = builder.Column<int>(nullable: false),
                DateAdded = builder.Column<DateTime>(nullable: false)
            }, constraints: table => table.PrimaryKey("PK_SuggestVotes", x => x.Id));

        migrationBuilder.CreateTable("SuggestThreads",
            builder => new
            {
                Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                MessageId = builder.Column<ulong>(nullable: false),
                ThreadChannelId = builder.Column<ulong>(nullable: false),
                DateAdded = builder.Column<DateTime>(nullable: false)
            }, constraints: table => table.PrimaryKey("PK_SuggestThreads", x => x.Id));
    }
}