using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class AddKarutaEventStuff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("KarutaButtonOptions", builder => new
        {
            Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            GuildId = builder.Column<ulong>(),
            Button1Text = builder.Column<string>(),
            Button2Text = builder.Column<string>(),
            Button3Text = builder.Column<string>(),
            Button4Text = builder.Column<string>(),
            Button5Text = builder.Column<string>(),
            Button6Text = builder.Column<string>(),
            DateAdded = builder.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_KarutaButtonOptions", x => x.Id));
        
        migrationBuilder.CreateTable("KarutaEventEntry", builder => new
        {
            Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            GuildId = builder.Column<ulong>(),
            ChannelId = builder.Column<ulong>(),
            MessageId = builder.Column<ulong>(),
            Button1Count = builder.Column<int>(),
            Button2Count = builder.Column<int>(),
            Button3Count = builder.Column<int>(),
            Button4Count = builder.Column<int>(),
            Button5Count = builder.Column<int>(),
            Button6Count = builder.Column<int>(),
            DateAdded = builder.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_KarutaEventEntry", x => x.Id));
        migrationBuilder.AddColumn<ulong>("KarutaEventChannel", "GuildConfigs", defaultValue: 0);
    }
    
}