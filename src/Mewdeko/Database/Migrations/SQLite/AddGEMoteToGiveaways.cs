using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class AddGEmoteToGiveaways : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Emote", "Giveaways", nullable: false,
            defaultValue: "<a:HaneMeow:914307922287276052>");
        migrationBuilder.Sql(
            "UPDATE GuildConfigs SET GiveawayEmote='<a:HaneMeow:914307922287276052>' WHERE GiveawayEmote is NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}