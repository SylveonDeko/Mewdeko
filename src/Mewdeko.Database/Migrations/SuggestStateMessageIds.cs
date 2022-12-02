using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations;

public partial class SuggestStateIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ulong>("StateChangeMessageId", "Suggestions", defaultValue: 0);
        migrationBuilder.AddColumn<int>("CurrentState", "Suggestions", defaultValue: 0);
        migrationBuilder.Sql("PRAGMA foreign_keys=off;"
                             + "\n ALTER TABLE Suggestions RENAME TO Suggestions_Old;"
                             + "\n CREATE TABLE Suggestions ("
                             + "\n Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,"
                             + "\n DateAdded TEXT NOT NULL,"
                             + "\n GuildId INTEGER NOT NULL,"
                             + "\n SuggestionId INTEGER NOT NULL DEFAULT 1,"
                             + "\n MessageId INTEGER NOT NULL,"
                             + "\n UserId INTEGER,"
                             + "\n Suggestion Text,"
                             + "\n EmoteCount1 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount2 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount3 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount4 INTEGER NOT NULL DEFAULT 0,"
                             + "\n EmoteCount5 INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeUser INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeCount INTEGER NOT NULL DEFAULT 0,"
                             + "\n StateChangeMessageId INTEGER NOT NULL DEFAULT 0,"
                             + "\n CurrentState INTEGER NOT NULL DEFAULT 0);"
                             + "\n INSERT INTO Suggestions (DateAdded, GuildId, SuggestionId, MessageId, UserId) SELECT DateAdded, GuildId, SuggestId, MessageId, UserId FROM Suggestions_Old;"
                             + "\n PRAGMA foreign_keys=on;");
        migrationBuilder.DropTable("Suggestions_Old");
    }
}