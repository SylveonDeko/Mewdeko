﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class LeaveHook : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                "LeaveHook",
                "GuildConfigs",
                "TEXT",
                defaultValue: 0,
                nullable: true);
            migrationBuilder.RenameColumn("WebhookURL", "GuildConfigs", "GreetHook");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                "GuildConfigs",
                "LeaveHook");
        }
    }
}