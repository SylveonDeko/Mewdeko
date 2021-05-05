using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class restoregcr : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE CustomReactions SET GuildId=null WHERE GuildId=''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
