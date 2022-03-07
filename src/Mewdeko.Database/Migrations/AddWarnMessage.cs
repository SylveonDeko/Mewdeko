using Microsoft.EntityFrameworkCore.Migrations;
using System.Linq.Expressions;

namespace Mewdeko.Database.Migrations;

public partial class AddWarnMessage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>("WarnMessage", "GuildConfig", defaultValue: "-");
}