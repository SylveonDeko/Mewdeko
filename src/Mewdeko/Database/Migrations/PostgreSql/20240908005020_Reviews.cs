#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class Reviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "BotReviews",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                AvatarUrl = table.Column<string>("text", nullable: false),
                Stars = table.Column<int>("integer", nullable: false),
                Review = table.Column<string>("text", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BotReviews", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "BotReviews");
    }
}