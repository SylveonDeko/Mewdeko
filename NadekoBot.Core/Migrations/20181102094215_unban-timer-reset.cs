using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class unbantimerreset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // I have to remove everything as it has been piling up for a year.
            migrationBuilder.Sql(@"DELETE FROM UnbanTimer;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
