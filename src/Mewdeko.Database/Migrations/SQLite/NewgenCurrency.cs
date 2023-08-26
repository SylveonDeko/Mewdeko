using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite
{
    public partial class NewGenCurrency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adding CurrencyName and CurrencyEmoji to GuildConfigs
            migrationBuilder.AddColumn<string>(
                name: "CurrencyName",
                table: "GuildConfigs",
                nullable: false,
                defaultValue: "Coins");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyEmoji",
                table: "GuildConfigs",
                nullable: false,
                defaultValue: "💰");

            // GlobalUserBalance Table
            migrationBuilder.CreateTable(
                name: "GlobalUserBalance",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                    UserId = table.Column<ulong>(nullable: false),
                    Balance = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalUserBalance", x => x.Id);
                });

            // GuildUserBalance Table
            migrationBuilder.CreateTable(
                name: "GuildUserBalance",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                    GuildId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    Balance = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildUserBalance", x => x.Id);
                });

            // TransactionHistory Table
            migrationBuilder.CreateTable(
                name: "TransactionHistory",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValue: DateTime.UtcNow),
                    GuildId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: true),
                    Amount = table.Column<long>(nullable: false),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionHistory", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Code to reverse the migrations
            migrationBuilder.DropTable("TransactionHistory");
            migrationBuilder.DropTable("GuildUserBalance");
            migrationBuilder.DropTable("GlobalUserBalance");
            migrationBuilder.DropColumn("CurrencyEmoji", "GuildConfigs");
            migrationBuilder.DropColumn("CurrencyName", "GuildConfigs");
        }
    }
}