using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SuggestStateIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("StateChangeMessageId", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<int>("CurrentState", "Suggestions", defaultValue: 0);
        migrationBuilder.Sql("PRAGMA foreign_keys=off;"
                             + "\nALTER TABLE Suggestions RENAME TO Suggestions_Old;"
                             + "\nCREATE TABLE Suggestions ("
                             + "\n DateAdded TEXT NOT NULL,"
                             + "\n GuildId INTEGER NOT NULL,"
                             + "\n SuggestID INTEGER NOT NULL DEFAULT 1,"
                             + "\n MessageID INTEGER NOT NULL,"
                             + "\n UserID INTEGER,"
                             + "\n Suggestion Text,"
                             + "\n EmoteCount1 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount2 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount3 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount4 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount5 INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeUser INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeCount INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeMessageId INTEGER NOT NULL DEFAULT 0,"
                             + "\n CurrentState INTEGER NOT NULL DEFAULT 0,"
                             + "\n PRIMARY KEY(MessageID));"
                             + "\n INSERT INTO Suggestions (DateAdded, GuildId, SuggestID, MessageId, UserId) SELECT DateAdded, GuildId, SuggestID, MessageId, UserId FROM Suggestions_Old;"
                             + "\n PRAGMA foreign_keys=on;");
        migrationBuilder.DropTable("Suggestions_Old");
    }
    
}