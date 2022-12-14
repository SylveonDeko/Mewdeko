using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class Confessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("Confessions",
            builder => new
            {
                Id = builder.Column<int>().Annotation("Sqlite:Autoincrement", true),
                GuildId = builder.Column<ulong>(nullable: false),
                UserId = builder.Column<ulong>(nullable: false),
                MessageId = builder.Column<ulong>(nullable: false),
                ChannelId = builder.Column<ulong>(nullable: false),
                ConfessNumber = builder.Column<ulong>(nullable: false),
                Confession = builder.Column<string>(nullable: false),
                DateAdded = builder.Column<DateTime>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Confessions", x => x.Id));
        migrationBuilder.AddColumn<ulong>("ConfessionLogChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ConfessionChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<string>("ConfessionBlacklist", "GuildConfigs", defaultValue: "0");
        migrationBuilder.CreateIndex(
            "IX_Confessions_GuildId",
            "Confessions",
            "UserId",
            unique: false);
    }
}