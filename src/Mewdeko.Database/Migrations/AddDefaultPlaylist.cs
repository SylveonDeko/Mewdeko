using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddDefaultPlaylist : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(
            "IsDefault",
            "MusicPlaylists",
            "INTEGER",
            defaultValue: 0,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}