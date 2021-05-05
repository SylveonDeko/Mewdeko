using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class currencymoved : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CurrencyAmount",
                table: "DiscordUser",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(@"UPDATE DiscordUser
SET CurrencyAmount=ifnull((SELECT Amount 
                    FROM Currency 
                    WHERE UserId=DiscordUser.UserId), 0);

INSERT INTO DiscordUser(UserId, Username, Discriminator, AvatarId)
    SELECT UserId, 'Unknown', '????', '' FROM Currency 
    WHERE UserId NOT IN (
        SELECT UserId FROM DiscordUser)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyAmount",
                table: "DiscordUser");

            //migrationBuilder.CreateTable(
            //    name: "Currency",
            //    columns: table => new
            //    {
            //        Id = table.Column<int>(nullable: false)
            //            .Annotation("Sqlite:Autoincrement", true),
            //        Amount = table.Column<long>(nullable: false),
            //        DateAdded = table.Column<DateTime>(nullable: true),
            //        UserId = table.Column<ulong>(nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Currency", x => x.Id);
            //    });

            //migrationBuilder.CreateIndex(
            //    name: "IX_Currency_UserId",
            //    table: "Currency",
            //    column: "UserId",
            //    unique: true);
        }
    }
}
