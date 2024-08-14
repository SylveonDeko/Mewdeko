using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MessageCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "TicketPanels");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "TicketPanels");

            migrationBuilder.AddColumn<string>(
                name: "MessageJson",
                table: "TicketPanels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseMessageCount",
                table: "GuildConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MessageCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Count = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                name: "MessageCounts");

            migrationBuilder.DropColumn(
                name: "MessageJson",
                table: "TicketPanels");

            migrationBuilder.DropColumn(
                name: "UseMessageCount",
                table: "GuildConfigs");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TicketPanels",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "TicketPanels",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
