using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class LogImprovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("UsernameUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ThreadCreatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("NicknameUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ThreadDeletedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("UserRoleAddedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("UserRoleRemovedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("AvatarUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ThreadUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("EventCreatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("RoleDeletedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("RoleUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("RoleCreatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<ulong>("ServerUpdatedId", "LogSettings", defaultValue: 0);
        migrationBuilder.AddColumn<string>("Usernames", "DiscordUser", defaultValue: null, nullable: true);
        migrationBuilder.AddColumn<string>("Nicknames", "DiscordUser", defaultValue: null, nullable: true);
    }
}