using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class cleanupfollowedstreams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM FollowedStream WHERE GuildConfigId is null");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
