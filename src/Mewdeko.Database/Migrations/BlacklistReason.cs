using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class BlacklistReason : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("Reason", "Blacklist", defaultValue: "No reason provided.", nullable: false);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("Reason", "Blacklist");
}