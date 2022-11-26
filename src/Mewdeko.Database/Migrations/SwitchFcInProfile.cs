using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SwitchFcInProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>("SwitchFriendCode", "DiscordUser", defaultValue: null, nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("SwitchFriendCode", "DiscordUser");
}
