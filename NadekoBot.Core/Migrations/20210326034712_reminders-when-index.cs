using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class reminderswhenindex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reminders_DateAdded",
                table: "Reminders");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_When",
                table: "Reminders",
                column: "When");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reminders_When",
                table: "Reminders");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_DateAdded",
                table: "Reminders",
                column: "DateAdded");
        }
    }
}
