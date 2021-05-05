using Microsoft.EntityFrameworkCore.Migrations;

namespace NadekoBot.Migrations
{
    public partial class cleanup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"DROP TABLE IF EXISTS Currency;

DELETE FROM GuildRepeater
WHERE GuildConfigId is null;

DELETE FROM AntiSpamIgnore
WHERE AntiSpamSettingId is null;

DELETE FROM BlacklistItem
WHERE BotConfigId is null;

DELETE FROM CommandAlias
WHERE GuildConfigId is null;

DELETE FROM CommandCooldown
WHERE GuildConfigId is null;

DELETE FROM DelMsgOnCmdChannel
WHERE GuildConfigId is null or ChannelId < 1000;

DELETE FROM BlockedCmdOrMdl
WHERE BotConfigId is null and BotConfigId1 is null;

DELETE FROM ExcludedItem
WHERE XpSettingsId is null;

DELETE FROM FilterChannelId 
WHERE GuildConfigId is null and GuildConfigId1 is null;

DELETE FROM FilteredWord
WHERE GuildConfigId is null;

DELETE FROM FollowedStream
WHERE GuildConfigId is null;

DELETE FROM GCChannelId
WHERE GuildConfigId is null;

DELETE FROM GroupName
WHERE GuildConfigId is null;

DELETE FROM MutedUserId
WHERE GuildConfigId is null;

DELETE FROM NsfwBlacklitedTag
WHERE GuildConfigId is null;

DELETE FROM Permissionv2
WHERE GUildconfigId is null;

DELETE FROM PlayingStatus
WHERE BotConfigId is null;

DELETE FROM PollVote
WHERE PollId is null;

DELETE FROM PollAnswer
WHERE PollId is null;

DELETE FROM ShopEntryItem
WHERE ShopEntryId in (SELECT Id from ShopEntry
	WHERE GuildConfigId is null);
	
DELETE FROM ShopEntry
WHERE GuildConfigId is null;

DELETE FROM SlowmodeIgnoredRole 
WHERE GuildConfigId is null;

DELETE FROM SlowmodeIgnoredUser
WHERE GuildConfigId is null;

DELETE FROM StartupCommand
WHERE BotConfigId is null;

DELETE FROM StreamRoleWhitelistedUser 
WHERE StreamRoleSettingsId is null;

DELETE FROM StreamRoleBlacklistedUser
WHERE StreamRoleSettingsId is null;

DELETE FROM UnbanTimer 
WHERE GuildConfigId is null;

DELETE FROM UnmuteTimer
WHERE GuildConfigId is null;

DELETE FROM VcRoleInfo
WHERE GuildConfigId is null;

DELETE FROM WarningPunishment
WHERE GuildConfigId is null;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
