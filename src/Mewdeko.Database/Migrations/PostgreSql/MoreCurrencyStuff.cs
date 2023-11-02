using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql;

public partial class MoreCurrencyStuff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("RewardAmount", "GuildConfigs", "bigint", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("RewardTimeoutSeconds", "GuildConfigs", "bigint", nullable: false,
            defaultValue: 86400);
        migrationBuilder.AddColumn<int>("RewardTimeoutSeconds", "OwnerOnly", "bigint", nullable: false,
            defaultValue: 86400);
        migrationBuilder.AddColumn<int>("RewardAmount", "OwnerOnly", "bigint", nullable: false, defaultValue: 0);
    }
}