using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Migrations
{
    public static class Helper
    {
        public static void BotconfigClear(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"INSERT INTO Blacklist(DateAdded, ItemID, Type) 
SELECT DateAdded, ItemId, Type FROM BlacklistItem;");

            migrationBuilder.Sql(
                @"INSERT INTO AutoCommands(DateAdded, CommandText, ChannelId, ChannelName, GuildId, GuildName, VoiceChannelId, VoiceChannelName, Interval) 
SELECT DateAdded, CommandText, ChannelId, ChannelName, GuildId, GuildName, VoiceChannelId, VoiceChannelName, Interval FROM StartupCommand;");

            migrationBuilder.Sql(@"INSERT INTO RotatingStatus(DateAdded, Status, Type) 
SELECT DateAdded, Status, Type FROM PlayingStatus;");
        }
    }
}