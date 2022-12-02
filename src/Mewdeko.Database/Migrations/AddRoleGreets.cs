using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddRoleGreets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("RoleGreets",
            table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>(),
                RoleId = table.Column<ulong>(),
                ChannelId = table.Column<ulong>(),
                Message = table.Column<string>(defaultValue: "Welcome %user%!"),
                DeleteTime = table.Column<ulong>(defaultValue: 1),
                GreetBots = table.Column<bool>(defaultValue: false),
                WebhookUrl = table.Column<string>(nullable: true),
                DateAdded = table.Column<DateTime>(nullable: true),
                Disabled = table.Column<bool>(defaultValue: false)
            }, constraints: table => table.PrimaryKey("PK_RoleGreets", x => x.Id));

        migrationBuilder.AddColumn<bool>("GreetBots", "MultiGreets", defaultValue: false);
    }
}