#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Mewdeko.Database.Migrations.PostgreSql;

/// <inheritdoc />
public partial class Tickets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "TicketPanels",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GuildId = table.Column<decimal>("numeric(20,0)", nullable: false),
                ChannelId = table.Column<decimal>("numeric(20,0)", nullable: false),
                Title = table.Column<string>("text", nullable: false),
                Description = table.Column<string>("text", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TicketPanels", x => x.Id);
            });

        migrationBuilder.CreateTable(
            "TicketButtons",
            table => new
            {
                Id = table.Column<int>("integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TicketPanelId = table.Column<int>("integer", nullable: false),
                Label = table.Column<string>("text", nullable: false),
                Emoji = table.Column<string>("text", nullable: false),
                OpenMessage = table.Column<string>("text", nullable: false),
                DateAdded = table.Column<DateTime>("timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TicketButtons", x => x.Id);
                table.ForeignKey(
                    "FK_TicketButtons_TicketPanels_TicketPanelId",
                    x => x.TicketPanelId,
                    "TicketPanels",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            "IX_TicketButtons_TicketPanelId",
            "TicketButtons",
            "TicketPanelId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            "TicketButtons");

        migrationBuilder.DropTable(
            "TicketPanels");
    }
}