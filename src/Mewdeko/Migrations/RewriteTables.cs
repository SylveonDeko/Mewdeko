using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class RewriteTables: Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Giveaways",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    When = table.Column<DateTime>(nullable: true),
                    ServerId = table.Column<ulong>(nullable: false),
                    ChannelId = table.Column<ulong>(nullable: true),
                    MessageId = table.Column<ulong>(nullable: true),
                    UserId = table.Column<ulong>(nullable: true),
                    Item = table.Column<string>(nullable: true),
                    RestrictTo = table.Column<string>(nullable: true),
                    BlacklistUsers = table.Column<string>(nullable: true),
                    BlacklistRoles = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Giveaways", x => x.Id);
                });
            migrationBuilder.CreateTable(
                name: "Ticket",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    GuildId = table.Column<ulong>(),
                    Creator = table.Column<ulong>(),
                    ChannelId = table.Column<ulong>(),
                    AddedUsers = table.Column<string>(defaultValue: "none"),
                    AddedRoles = table.Column<string>(defaultValue: "none"),
                    ClaimedBy = table.Column<ulong>(defaultValue: 0),
                    ClosedBy = table.Column<ulong>(defaultValue: 0),
                    TicketNumber = table.Column<ulong>()

                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });
            migrationBuilder.AddColumn<string>("GRolesBlacklist", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("GUsersBlacklist", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("GStartMessage", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("GEndMessage", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("GWinMessage", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("AcceptMotes", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("DenyMotes", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("ImplementMotes", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("ConsiderMotes", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<string>("BanChannelMessage", "GuildConfigs", defaultValue: "-");
            migrationBuilder.AddColumn<ulong>("TicketNumber", "GuildConfigs", defaultValue: "1");
            migrationBuilder.AddColumn<ulong>("TicketChannel", "GuildConfigs", defaultValue: 0);
            migrationBuilder.AddColumn<string>("TOpenMessage", "GuildConfigs", defaultValue: "none");

            migrationBuilder.CreateIndex(
                name: "IX_Giveaways_GuildId",
                table: "Giveaways",
                column: "ServerId",
                unique: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Giveaways");
            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}