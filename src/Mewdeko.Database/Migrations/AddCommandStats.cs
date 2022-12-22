using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddCommandStats : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("CommandStats", columns => new
        {
            Id = columns.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            NameOrId = columns.Column<string>(),
            Module = columns.Column<string>(nullable: true),
            Trigger = columns.Column<bool>(defaultValue: false),
            IsSlash = columns.Column<bool>(defaultValue: false),
            UserId = columns.Column<ulong>(),
            GuildId = columns.Column<ulong>(),
            ChannelId = columns.Column<ulong>(),
            DateAdded = columns.Column<DateTime>()
        }, constraints: table => table.PrimaryKey("PK_CommandStats", x => x.Id));
        migrationBuilder.AddColumn<bool>("StatsOptOut", "GuildConfigs", defaultValue: false);
        migrationBuilder.AddColumn<bool>("StatsOptOut", "DiscordUser", defaultValue: false);
    }
}