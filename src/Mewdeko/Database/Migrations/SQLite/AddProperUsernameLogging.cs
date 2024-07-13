using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.SQLite;
/// <inheritdoc />
public partial class AddProperUsernameLogging : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable("Usernames",
            builder => new
            {
                Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                Username = builder.Column<string>(nullable: true),
                UserId = builder.Column<ulong>(),
                DateAdded = builder.Column<DateTime>(nullable: true)
            }, constraints: table => table.PrimaryKey("PK_Usernames", x => x.Id));
}