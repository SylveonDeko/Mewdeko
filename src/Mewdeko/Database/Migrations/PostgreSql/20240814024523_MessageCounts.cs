#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class MessageCounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            "Description",
            "TicketPanels");

        migrationBuilder.DropColumn(
            "Title",
            "TicketPanels");

        migrationBuilder.AddColumn<string>(
            "MessageJson",
            "TicketPanels",
            "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            "UseMessageCount",
            "GuildConfigs",
            "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            "MessageCounts",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                UserId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Count = table.Column<decimal>("numeric(20,0)", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageCounts", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "MessageCounts");

        migrationBuilder.DropColumn(
            "MessageJson",
            "TicketPanels");

        migrationBuilder.DropColumn(
            "UseMessageCount",
            "GuildConfigs");

        migrationBuilder.AddColumn<string>(
            "Description",
            "TicketPanels",
            "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            "Title",
            "TicketPanels",
            "text",
            nullable: false,
            defaultValue: "");
    }
}