using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class ValidTriggerTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<int>("ValidTriggerTypes", "ChatTriggers", nullable: false, defaultValue: (ChatTriggerType)0b1111);
}