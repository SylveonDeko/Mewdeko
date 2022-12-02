using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class CleanAndRemakePolls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "PRAGMA foreign_keys=off; delete from PollAnswer; delete from Poll; DELETE from PollVote; PRAGMA foreign_keys=on;");
        migrationBuilder.AddColumn<int>("PollType", "Poll");
    }
}