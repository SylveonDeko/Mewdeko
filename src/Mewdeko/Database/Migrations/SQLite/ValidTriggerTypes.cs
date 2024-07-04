using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;

public partial class ValidTriggerTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<int>("ValidTriggerTypes", "ChatTriggers", nullable: false, defaultValue: 15);
}