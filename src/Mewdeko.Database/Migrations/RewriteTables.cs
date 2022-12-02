using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class RewriteTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "Giveaways",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                When = table.Column<DateTime>(nullable: true),
                ServerId = table.Column<ulong>(nullable: false),
                Ended = table.Column<int>(),
                ChannelId = table.Column<ulong>(nullable: true),
                MessageId = table.Column<ulong>(nullable: true),
                UserId = table.Column<ulong>(nullable: true),
                Item = table.Column<string>(nullable: true),
                RestrictTo = table.Column<string>(nullable: true),
                BlacklistUsers = table.Column<string>(nullable: true),
                BlacklistRoles = table.Column<string>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Giveaways", x => x.Id));
        migrationBuilder.CreateTable(
            "Tickets",
            table => new
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
            constraints: table => table.PrimaryKey("PK_Tickets", x => x.Id));
        migrationBuilder.CreateTable(
            "StatusRoles",
            table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DateAdded = table.Column<DateTime>(nullable: true),
                GuildId = table.Column<ulong>(),
                Status = table.Column<string>(),
                ToAdd = table.Column<string>(nullable: true),
                ToRemove = table.Column<string>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_StatusRoles", x => x.Id));
        migrationBuilder.AddColumn<ulong>("CleverbotChannel", "GuildConfigs", defaultValue: 0);
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
        migrationBuilder.AddColumn<int>("BanChannelMessageType", "GuildConfigs", defaultValue: 1);
        migrationBuilder.AddColumn<ulong>("TicketNumber", "GuildConfigs", defaultValue: "1");
        migrationBuilder.AddColumn<ulong>("TicketChannel", "GuildConfigs", defaultValue: 0);
        migrationBuilder.AddColumn<string>("TicketChannelName", "GuildConfigs", defaultValue: "ticket-%ticket.number%");
        migrationBuilder.AddColumn<string>("TOpenMessage", "GuildConfigs", defaultValue: "none");
        migrationBuilder.AddColumn<string>("XPImage", "GuildConfigs", defaultValue: "none");

        migrationBuilder.CreateIndex(
            "IX_Giveaways_GuildId",
            "Giveaways",
            "ServerId",
            unique: false);
        migrationBuilder.CreateIndex(
            "IX_Tickets_GuildId",
            "Tickets",
            "GuildId",
            unique: false);
        migrationBuilder.CreateIndex(
            "IX_StatusRoles_GuildId",
            "StatusRoles",
            "GuildId",
            unique: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "Giveaways");
        migrationBuilder.DropTable(
            "Tickets");
    }
}