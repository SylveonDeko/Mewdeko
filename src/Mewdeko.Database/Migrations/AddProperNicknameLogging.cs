using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddProperNicknameLogging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable("Nicknames",
            builder => new
            {
                Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                GuildId = builder.Column<ulong>(),
                Nickname = builder.Column<string>(nullable: true),
                UserId = builder.Column<ulong>(),
                DateAdded = builder.Column<DateTime>(nullable: true)
            }, constraints: table => table.PrimaryKey("PK_Nicknames", x => x.Id));
}