using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class SeparateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BotUrl = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoggingV2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LogOtherId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MessageDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ThreadUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UsernameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    NicknameUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AvatarUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserLeftId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserBannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUnbannedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserJoinedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserRoleAddedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserRoleRemovedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserMutedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogUserPresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresenceId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LogVoicePresenceTTSId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ServerUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleDeletedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    EventCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoleCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelCreatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelDestroyedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ChannelUpdatedId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoggingV2", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotInstances");

            migrationBuilder.DropTable(
                name: "LoggingV2");
        }
    }
}
