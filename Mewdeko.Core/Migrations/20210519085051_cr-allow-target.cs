using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public partial class crallowtarget : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowTarget",
                table: "CustomReactions",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("update customreactions set allowtarget=1 where instr(lower(Response), '%target%') > 0");
            migrationBuilder.Sql("update customreactions set Trigger=replace(Trigger, '%mention%', '%bot.mention%')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowTarget",
                table: "CustomReactions");
        }
    }
}
