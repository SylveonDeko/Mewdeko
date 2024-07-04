using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class MoreCurrencyStuff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("RewardAmount", "GuildConfigs", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("RewardTimeoutSeconds", "GuildConfigs", nullable: false, defaultValue: 86400);
        migrationBuilder.AddColumn<int>("RewardTimeoutSeconds", "OwnerOnly", nullable: false, defaultValue: 86400);
        migrationBuilder.AddColumn<int>("RewardAmount", "OwnerOnly", nullable: false, defaultValue: 0);
    }
}