using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class Reviews2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "BotReviews",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                table: "BotReviews");
        }
    }
}
