using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddMultiGreets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "MultiGreets",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GuildId = table.Column<ulong>(),
                ChannelId = table.Column<ulong>(),
                Message = table.Column<string>(defaultValue: "Welcome %user%!"),
                DeleteTime = table.Column<ulong>(defaultValue: 1),
                WebhookUrl = table.Column<string>(nullable: true),
                DateAdded = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_MultiGreet", x => x.Id));
        migrationBuilder.AddColumn<int>("MultiGreetType", "GuildConfigs", defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("MultiGreets");
}