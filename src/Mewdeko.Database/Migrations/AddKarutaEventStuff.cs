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
            Button1Text = builder.Column<string>(nullable: true),
            Button2Text = builder.Column<string>(nullable: true),
            Button3Text = builder.Column<string>(nullable: true),
            Button4Text = builder.Column<string>(nullable: true),
            Button5Text = builder.Column<string>(nullable: true),
            Button6Text = builder.Column<string>(nullable: true),
            DateAdded = builder.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_KarutaButtonOptions", x => x.Id));
        
        migrationBuilder.CreateTable("KarutaEventEntry", builder => new
        {
            Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            GuildId = builder.Column<ulong>(),
            ChannelId = builder.Column<ulong>(),
            MessageId = builder.Column<ulong>(),
            EntryNumber = builder.Column<int>(),
            Button1Count = builder.Column<int>(nullable: true),
            Button2Count = builder.Column<int>(nullable: true),
            Button3Count = builder.Column<int>(nullable: true),
            Button4Count = builder.Column<int>(nullable: true),
            Button5Count = builder.Column<int>(nullable: true),
            Button6Count = builder.Column<int>(nullable: true),
            DateAdded = builder.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_KarutaEventEntry", x => x.Id));
        migrationBuilder.AddColumn<ulong>("KarutaEventChannel", "GuildConfigs", defaultValue: 0);
        
        migrationBuilder.CreateTable("KarutaEventVotes", builder => new
        {
            Id = builder.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            GuildId = builder.Column<ulong>(),
            MessageId = builder.Column<ulong>(),
            UserId = builder.Column<ulong>(),
            VotedNum = builder.Column<int>(),
            DateAdded = builder.Column<DateTime>(nullable: true)
        }, constraints: table => table.PrimaryKey("PK_KarutaEventVotes", x => x.Id));
        
    }
    
}