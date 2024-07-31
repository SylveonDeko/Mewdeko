        using System.ComponentModel.DataAnnotations.Schema;
        using Mewdeko.Database.Common;

        #pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        // ReSharper disable InconsistentNaming

        namespace Mewdeko.Database.Models
        {
            /// <summary>
            /// Represents the configuration settings for a guild.
            /// </summary>
            public class GuildConfig : DbEntity
            {
                /// <summary>
                /// Gets or sets the guild ID.
                /// </summary>
                public ulong GuildId { get; set; }

                /// <summary>
                /// Gets or sets the command prefix for the guild.
                /// </summary>
                public string? Prefix { get; set; } = "";

                /// <summary>
                /// Gets or sets the role ID for staff members.
                /// </summary>
                public ulong StaffRole { get; set; }

                /// <summary>
                /// Gets or sets the role ID for game masters.
                /// </summary>
                public ulong GameMasterRole { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for command logs.
                /// </summary>
                public ulong CommandLogChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets a value indicating whether to delete messages after a command is issued.
                /// </summary>
                public bool DeleteMessageOnCommand { get; set; } = false;

                /// <summary>
                /// Gets or sets the message for warnings.
                /// </summary>
                public string? WarnMessage { get; set; } = "-";

                /// <summary>
                /// Gets or sets the channels where messages are deleted after a command is issued.
                /// </summary>
                public HashSet<DelMsgOnCmdChannel> DelMsgOnCmdChannels { get; set; } = [];

                /// <summary>
                /// Gets or sets the role ID to auto-assign to new members.
                /// </summary>
                public string? AutoAssignRoleId { get; set; } = "0";

                /// <summary>
                /// Gets or sets the URL for the XP image.
                /// </summary>
                public string? XpImgUrl { get; set; }

                /// <summary>
                /// Gets or sets a value indicating whether to opt out of statistics tracking.
                /// </summary>
                public bool StatsOptOut { get; set; } = false;

                /// <summary>
                /// Gets or sets the name of the guild currency.
                /// </summary>
                public string? CurrencyName { get; set; } = "Coins";

                /// <summary>
                /// Gets or sets the emoji used for the guild currency.
                /// </summary>
                public string? CurrencyEmoji { get; set; } = "💰";

                /// <summary>
                /// Gets or sets the reward amount for certain actions.
                /// </summary>
                public int RewardAmount { get; set; } = 200;

                /// <summary>
                /// Gets or sets the reward timeout in seconds.
                /// </summary>
                public int RewardTimeoutSeconds { get; set; } = 86400;

                /// <summary>
                /// Gets or sets the URL for the giveaway banner.
                /// </summary>
                public string? GiveawayBanner { get; set; } = "";

                /// <summary>
                /// Gets or sets the color for the giveaway embed.
                /// </summary>
                public string? GiveawayEmbedColor { get; set; } = "";

                /// <summary>
                /// Gets or sets the color for the giveaway win embed.
                /// </summary>
                public string? GiveawayWinEmbedColor { get; set; } = "";

                /// <summary>
                /// Gets or sets a value indicating whether to send a direct message to the winner of a giveaway.
                /// </summary>
                public bool DmOnGiveawayWin { get; set; } = true;

                /// <summary>
                /// Gets or sets the end message for giveaways.
                /// </summary>
                public string? GiveawayEndMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the role to ping for giveaways.
                /// </summary>
                public ulong GiveawayPingRole { get; set; } = 0;

                /// <summary>
                /// Gets or sets a value indicating whether bots are allowed in the starboard.
                /// </summary>
                public bool StarboardAllowBots { get; set; } = true;

                /// <summary>
                /// Gets or sets a value indicating whether to remove starboard entries when the original message is deleted.
                /// </summary>
                public bool StarboardRemoveOnDelete { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to remove starboard entries when reactions are cleared.
                /// </summary>
                public bool StarboardRemoveOnReactionsClear { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to remove starboard entries when they fall below the threshold.
                /// </summary>
                public bool StarboardRemoveOnBelowThreshold { get; set; } = true;

                /// <summary>
                /// Gets or sets a value indicating whether to use a blacklist for the starboard.
                /// </summary>
                public bool UseStarboardBlacklist { get; set; } = true;

                /// <summary>
                /// Gets or sets the channels to check for starboard entries.
                /// </summary>
                public string? StarboardCheckChannels { get; set; } = "0";

                /// <summary>
                /// Gets or sets the password for voting.
                /// </summary>
                public string? VotesPassword { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for votes.
                /// </summary>
                public ulong VotesChannel { get; set; }

                /// <summary>
                /// Gets or sets the embed for votes.
                /// </summary>
                public string? VoteEmbed { get; set; }

                /// <summary>
                /// Gets or sets the type of suggestion threads.
                /// </summary>
                public int SuggestionThreadType { get; set; } = 0;

                /// <summary>
                /// Gets or sets a value indicating whether to archive suggestions on denial.
                /// </summary>
                public bool ArchiveOnDeny { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to archive suggestions on acceptance.
                /// </summary>
                public bool ArchiveOnAccept { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to archive suggestions on consideration.
                /// </summary>
                public bool ArchiveOnConsider { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to archive suggestions on implementation.
                /// </summary>
                public bool ArchiveOnImplement { get; set; } = false;

                /// <summary>
                /// Gets or sets the message for the suggestion button.
                /// </summary>
                public string? SuggestButtonMessage { get; set; } = "-";

                /// <summary>
                /// Gets or sets the name for the suggestion button.
                /// </summary>
                public string? SuggestButtonName { get; set; } = "-";

                /// <summary>
                /// Gets or sets the emote for the suggestion button.
                /// </summary>
                public string? SuggestButtonEmote { get; set; } = "-";

                /// <summary>
                /// Gets or sets the repost threshold for the button.
                /// </summary>
                public int ButtonRepostThreshold { get; set; } = 5;

                /// <summary>
                /// Gets or sets the type for suggestion commands.
                /// </summary>
                public int SuggestCommandsType { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for accepted suggestions.
                /// </summary>
                public ulong AcceptChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for denied suggestions.
                /// </summary>
                public ulong DenyChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for considered suggestions.
                /// </summary>
                public ulong ConsiderChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for implemented suggestions.
                /// </summary>
                public ulong ImplementChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the mode for emotes.
                /// </summary>
                public int EmoteMode { get; set; } = 0;

                /// <summary>
                /// Gets or sets the message for suggestions.
                /// </summary>
                public string? SuggestMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the message for denied suggestions.
                /// </summary>
                public string? DenyMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the message for accepted suggestions.
                /// </summary>
                public string? AcceptMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the message for implemented suggestions.
                /// </summary>
                public string? ImplementMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the message for considered suggestions.
                /// </summary>
                public string? ConsiderMessage { get; set; } = "";

                /// <summary>
                /// Gets or sets the minimum length for suggestions.
                /// </summary>
                public int MinSuggestLength { get; set; } = 0;

                /// <summary>
                /// Gets or sets the maximum length for suggestions.
                /// </summary>
                public int MaxSuggestLength { get; set; } = 4098;

                /// <summary>
                /// Gets or sets the emotes for suggestions.
                /// </summary>
                public string? SuggestEmotes { get; set; }

                /// <summary>
                /// Gets or sets the suggestion number.
                /// </summary>
                public ulong sugnum { get; set; } = 1;

                /// <summary>
                /// Gets or sets the suggestion channel ID.
                /// </summary>
                public ulong sugchan { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for the suggestion button.
                /// </summary>
                public ulong SuggestButtonChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the style for the first emote.
                /// </summary>
                public int Emote1Style { get; set; } = 2;

                /// <summary>
                /// Gets or sets the style for the second emote.
                /// </summary>
                public int Emote2Style { get; set; } = 2;

                /// <summary>
                /// Gets or sets the style for the third emote.
                /// </summary>
                public int Emote3Style { get; set; } = 2;

                /// <summary>
                /// Gets or sets the style for the fourth emote.
                /// </summary>
                public int Emote4Style { get; set; } = 2;

                /// <summary>
                /// Gets or sets the style for the fifth emote.
                /// </summary>
                public int Emote5Style { get; set; } = 2;

                /// <summary>
                /// Gets or sets the message ID for the suggestion button.
                /// </summary>
                public ulong SuggestButtonMessageId { get; set; } = 0;

                /// <summary>
                /// Gets or sets the repost threshold for the suggestion button.
                /// </summary>
                public int SuggestButtonRepostThreshold { get; set; } = 5;

                /// <summary>
                /// Gets or sets the color for the suggestion button.
                /// </summary>
                public int SuggestButtonColor { get; set; } = 2;

                /// <summary>
                /// Gets or sets the AFK message.
                /// </summary>
                public string? AfkMessage { get; set; } = "-";

                /// <summary>
                /// Gets or sets the auto bot role IDs.
                /// </summary>
                public string? AutoBotRoleIds { get; set; }

                /// <summary>
                /// Gets or sets a value indicating whether the guild bot is enabled.
                /// </summary>
                public int GBEnabled { get; set; } = 1;

                /// <summary>
                /// Gets or sets a value indicating the action for the guild bot.
                /// </summary>
                public bool GBAction { get; set; } = false;

                /// <summary>
                /// Gets or sets the channel ID for confession logs.
                /// </summary>
                public ulong ConfessionLogChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for confessions.
                /// </summary>
                public ulong ConfessionChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the blacklist for confessions.
                /// </summary>
                public string? ConfessionBlacklist { get; set; } = "0";

                /// <summary>
                /// Gets or sets the type for multi-greet.
                /// </summary>
                public int MultiGreetType { get; set; } = 0;

                /// <summary>
                /// Gets or sets the member role ID.
                /// </summary>
                public ulong MemberRole { get; set; } = 0;

                /// <summary>
                /// Gets or sets the open message for tickets.
                /// </summary>
                public string? TOpenMessage { get; set; } = "none";

                /// <summary>
                /// Gets or sets the start message for giveaways.
                /// </summary>
                public string? GStartMessage { get; set; } = "none";

                /// <summary>
                /// Gets or sets the end message for giveaways.
                /// </summary>
                public string? GEndMessage { get; set; } = "none";

                /// <summary>
                /// Gets or sets the win message for giveaways.
                /// </summary>
                public string? GWinMessage { get; set; } = "none";

                /// <summary>
                /// Gets or sets the channel ID for warning logs.
                /// </summary>
                public ulong WarnlogChannelId { get; set; } = 0;

                /// <summary>
                /// Gets or sets the channel ID for mini warning logs.
                /// </summary>
                public ulong MiniWarnlogChannelId { get; set; } = 0;

                /// <summary>
                /// Gets or sets a value indicating whether to send a message when the server is boosted.
                /// </summary>
                public bool SendBoostMessage { get; set; } = false;

                /// <summary>
                /// Gets or sets the blacklist for roles for the guild bot.
                /// </summary>
                public string? GRolesBlacklist { get; set; } = "-";

                /// <summary>
                /// Gets or sets the blacklist for users for the guild bot.
                /// </summary>
                public string? GUsersBlacklist { get; set; } = "-";

                /// <summary>
                /// Gets or sets the message for when the server is boosted.
                /// </summary>
                public string? BoostMessage { get; set; } = "%user% just boosted this server!";

                /// <summary>
                /// Gets or sets the channel ID for boost messages.
                /// </summary>
                public ulong BoostMessageChannelId { get; set; }

                /// <summary>
                /// Gets or sets the time in seconds after which the boost message is deleted.
                /// </summary>
                public int BoostMessageDeleteAfter { get; set; }

                /// <summary>
                /// Gets or sets the emote for giveaways.
                /// </summary>
                public string? GiveawayEmote { get; set; } = "🎉";

                /// <summary>
                /// Gets or sets the channel ID for tickets.
                /// </summary>
                public ulong TicketChannel { get; set; } = 0;

                /// <summary>
                /// Gets or sets the category ID for tickets.
                /// </summary>
                public ulong TicketCategory { get; set; } = 0;

                /// <summary>
                /// Gets or sets a value indicating whether the snipe feature is enabled.
                /// </summary>
                public bool snipeset { get; set; } = false;

                /// <summary>
                /// Gets or sets the length of time in seconds after which a user is considered AFK.
                /// </summary>
                public int AfkLength { get; set; } = 250;

                /// <summary>
                /// Gets or sets the timeout for XP in text channels.
                /// </summary>
                public int XpTxtTimeout { get; set; }

                /// <summary>
                /// Gets or sets the rate of XP gain in text channels.
                /// </summary>
                public int XpTxtRate { get; set; }

                /// <summary>
                /// Gets or sets the rate of XP gain in voice channels.
                /// </summary>
                public int XpVoiceRate { get; set; }

                /// <summary>
                /// Gets or sets the timeout for XP in voice channels.
                /// </summary>
                public int XpVoiceTimeout { get; set; }

                /// <summary>
                /// Gets or sets the warning punishments.
                /// </summary>
                public List<WarningPunishment2> WarnPunishments2 { get; set; } = [];

                /// <summary>
                /// Gets or sets the number of stars for the guild.
                /// </summary>
                public int Stars { get; set; } = 3;

                /// <summary>
                /// Gets or sets the AFK type.
                /// </summary>
                public int AfkType { get; set; } = 2;

                /// <summary>
                /// Gets or sets the anti-alt settings.
                /// </summary>
                public AntiAltSetting AntiAltSetting { get; set; }

                /// <summary>
                /// Gets or sets the channels where AFK is disabled.
                /// </summary>
                public string? AfkDisabledChannels { get; set; }

                /// <summary>
                /// Gets or sets the AFK delete message.
                /// </summary>
                public string? AfkDel { get; set; }

                /// <summary>
                /// Gets or sets the AFK timeout in seconds.
                /// </summary>
                public int AfkTimeout { get; set; } = 20;

                /// <summary>
                /// Gets or sets the number of joins for the guild.
                /// </summary>
                public ulong Joins { get; set; }

                /// <summary>
                /// Gets or sets the number of leaves for the guild.
                /// </summary>
                public ulong Leaves { get; set; }

                /// <summary>
                /// Gets or sets the star emote for the guild.
                /// </summary>
                public string? Star2 { get; set; } = "⭐";

                /// <summary>
                /// Gets or sets the channel ID for the starboard.
                /// </summary>
                public ulong StarboardChannel { get; set; }

                /// <summary>
                /// Gets or sets the repost threshold for the starboard.
                /// </summary>
                public int RepostThreshold { get; set; }

                /// <summary>
                /// Gets or sets the preview links setting.
                /// </summary>
                public int PreviewLinks { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for reactions.
                /// </summary>
                public ulong ReactChannel { get; set; }

                /// <summary>
                /// Gets or sets the warning count for filter warnings.
                /// </summary>
                public int fwarn { get; set; }

                /// <summary>
                /// Gets or sets the warning count for invite filter warnings.
                /// </summary>
                public int invwarn { get; set; }

                /// <summary>
                /// Gets or sets the number of roles to remove.
                /// </summary>
                public int removeroles { get; set; }

                /// <summary>
                /// Gets or sets a value indicating whether to automatically delete greet messages.
                /// </summary>
                public bool AutoDeleteGreetMessages { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to automatically delete bye messages.
                /// </summary>
                public bool AutoDeleteByeMessages { get; set; } = false;

                /// <summary>
                /// Gets or sets the timer for automatically deleting greet messages.
                /// </summary>
                public int AutoDeleteGreetMessagesTimer { get; set; } = 30;

                /// <summary>
                /// Gets or sets the timer for automatically deleting bye messages.
                /// </summary>
                public int AutoDeleteByeMessagesTimer { get; set; } = 30;

                /// <summary>
                /// Gets or sets the channel ID for greet messages.
                /// </summary>
                public ulong GreetMessageChannelId { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for bye messages.
                /// </summary>
                public ulong ByeMessageChannelId { get; set; }

                /// <summary>
                /// Gets or sets the webhook URL for greet messages.
                /// </summary>
                public string? GreetHook { get; set; } = "";

                /// <summary>
                /// Gets or sets the webhook URL for leave messages.
                /// </summary>
                public string? LeaveHook { get; set; } = "";

                /// <summary>
                /// Gets or sets a value indicating whether to send a direct message for greet messages.
                /// </summary>
                public bool SendDmGreetMessage { get; set; } = false;

                /// <summary>
                /// Gets or sets the text for direct message greet messages.
                /// </summary>
                public string? DmGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

                /// <summary>
                /// Gets or sets a value indicating whether to send a channel greet message.
                /// </summary>
                public bool SendChannelGreetMessage { get; set; } = false;

                /// <summary>
                /// Gets or sets the text for channel greet messages.
                /// </summary>
                public string? ChannelGreetMessageText { get; set; } = "Welcome to the %server% server, %user%!";

                /// <summary>
                /// Gets or sets a value indicating whether to send a channel bye message.
                /// </summary>
                public bool SendChannelByeMessage { get; set; } = false;

                /// <summary>
                /// Gets or sets the text for channel bye messages.
                /// </summary>
                public string? ChannelByeMessageText { get; set; } = "%user% has left!";

                /// <summary>
                /// Gets or sets the log settings for the guild.
                /// </summary>
                public LogSetting LogSetting { get; set; } = new();

                /// <summary>
                /// Gets or sets a value indicating whether to restrict self-assigned roles to be exclusive.
                /// </summary>
                public bool ExclusiveSelfAssignedRoles { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to automatically delete self-assigned role messages.
                /// </summary>
                public bool AutoDeleteSelfAssignedRoleMessages { get; set; } = false;

                /// <summary>
                /// Gets or sets the ID for the log setting.
                /// </summary>
                [ForeignKey("LogSettingId")]
                public int? LogSettingId { get; set; }

                /// <summary>
                /// Gets or sets the followed streams for the guild.
                /// </summary>
                public HashSet<FollowedStream> FollowedStreams { get; set; } = [];

                /// <summary>
                /// Gets or sets the permissions for the guild.
                /// </summary>
                public List<Permissionv2>? Permissions { get; set; }

                /// <summary>
                /// Gets or sets a value indicating whether to use verbose permissions.
                /// </summary>
                public bool VerbosePermissions { get; set; } = true;

                /// <summary>
                /// Gets or sets the role for permissions.
                /// </summary>
                public string? PermissionRole { get; set; } = null;

                /// <summary>
                /// Gets or sets the command cooldowns for the guild.
                /// </summary>
                public HashSet<CommandCooldown> CommandCooldowns { get; set; } = [];

                /// <summary>
                /// Gets or sets a value indicating whether to filter invites.
                /// </summary>
                public bool FilterInvites { get; set; } = false;

                /// <summary>
                /// Gets or sets a value indicating whether to filter links.
                /// </summary>
                public bool FilterLinks { get; set; } = false;

                /// <summary>
                /// Gets or sets the channel IDs for filtering invites.
                /// </summary>
                public HashSet<FilterInvitesChannelIds> FilterInvitesChannelIds { get; set; } = [];

                /// <summary>
                /// Gets or sets the channel IDs for filtering links.
                /// </summary>
                public HashSet<FilterLinksChannelId> FilterLinksChannelIds { get; set; } = [];

                /// <summary>
                /// Gets or sets a value indicating whether to filter words.
                /// </summary>
                public bool FilterWords { get; set; } = false;

                /// <summary>
                /// Gets or sets the words to be filtered.
                /// </summary>
                public HashSet<FilteredWord> FilteredWords { get; set; } = [];

                /// <summary>
                /// Gets or sets the channel IDs for filtering words.
                /// </summary>
                public HashSet<FilterWordsChannelIds> FilterWordsChannelIds { get; set; } = [];

                /// <summary>
                /// Gets or sets the muted users for the guild.
                /// </summary>
                public HashSet<MutedUserId> MutedUsers { get; set; } = [];

                /// <summary>
                /// Gets or sets the name of the mute role.
                /// </summary>
                public string? MuteRoleName { get; set; }

                /// <summary>
                /// Gets or sets the channel ID for Cleverbot.
                /// </summary>
                public ulong CleverbotChannel { get; set; }

                /// <summary>
                /// Gets or sets the repeaters for the guild.
                /// </summary>
                public List<Repeater> GuildRepeaters { get; set; } = [];

                /// <summary>
                /// Gets or sets the anti-raid settings for the guild.
                /// </summary>
                public AntiRaidSetting AntiRaidSetting { get; set; }

                /// <summary>
                /// Gets or sets the anti-spam settings for the guild.
                /// </summary>
                public AntiSpamSetting AntiSpamSetting { get; set; }

                /// <summary>
                /// Gets or sets the locale for the guild.
                /// </summary>
                public string? Locale { get; set; } = null;

                /// <summary>
                /// Gets or sets the time zone ID for the guild.
                /// </summary>
                public string? TimeZoneId { get; set; } = null;

                /// <summary>
                /// Gets or sets the unmute timers for the guild.
                /// </summary>
                public HashSet<UnmuteTimer> UnmuteTimers { get; set; } = [];

                /// <summary>
                /// Gets or sets the unban timers for the guild.
                /// </summary>
                public HashSet<UnbanTimer> UnbanTimer { get; set; } = [];

                /// <summary>
                /// Gets or sets the unrole timers for the guild.
                /// </summary>
                public HashSet<UnroleTimer> UnroleTimer { get; set; } = [];

                /// <summary>
                /// Gets or sets the voice channel role information for the guild.
                /// </summary>
                public HashSet<VcRoleInfo> VcRoleInfos { get; set; }

                /// <summary>
                /// Gets or sets the command aliases for the guild.
                /// </summary>
                public HashSet<CommandAlias> CommandAliases { get; set; } = [];

                /// <summary>
                /// Gets or sets the warning punishments for the guild.
                /// </summary>
                public List<WarningPunishment> WarnPunishments { get; set; } = [];

                /// <summary>
                /// Gets or sets a value indicating whether warnings have been initialized.
                /// </summary>
                public bool WarningsInitialized { get; set; } = false;

                /// <summary>
                /// Gets or sets the NSFW blacklisted tags for the guild.
                /// </summary>
                public HashSet<NsfwBlacklitedTag> NsfwBlacklistedTags { get; set; } = [];

                /// <summary>
                /// Gets or sets the game voice channel ID.
                /// </summary>
                public ulong? GameVoiceChannel { get; set; } = null;

                /// <summary>
                /// Gets or sets a value indicating whether to use verbose errors.
                /// </summary>
                public bool VerboseErrors { get; set; } = true;

                /// <summary>
                /// Gets or sets the stream role settings for the guild.
                /// </summary>
                public StreamRoleSettings? StreamRole { get; set; }

                /// <summary>
                /// Gets or sets the XP settings for the guild.
                /// </summary>
                public XpSettings? XpSettings { get; set; }

                /// <summary>
                /// Gets or sets the feed subscriptions for the guild.
                /// </summary>
                public List<FeedSub> FeedSubs { get; set; } = [];

                /// <summary>
                /// Gets or sets the reaction role messages for the guild.
                /// </summary>
                public IndexedCollection<ReactionRoleMessage> ReactionRoleMessages { get; set; } = [];

                /// <summary>
                /// Gets or sets a value indicating whether to notify when the stream goes offline.
                /// </summary>
                public bool NotifyStreamOffline { get; set; } = false;

                /// <summary>
                /// Gets or sets the self-assignable role group names for the guild.
                /// </summary>
                public List<GroupName> SelfAssignableRoleGroupNames { get; set; }

                /// <summary>
                /// Gets or sets the number of hours after which warnings expire.
                /// </summary>
                public int WarnExpireHours { get; set; } = 0;

                /// <summary>
                /// Gets or sets the action to take when warnings expire.
                /// </summary>
                public WarnExpireAction WarnExpireAction { get; set; } = WarnExpireAction.Clear;

                /// <summary>
                /// Gets or sets the color for the join graph.
                /// </summary>
                public uint JoinGraphColor { get; set; } = 4294956800;

                /// <summary>
                /// Gets or sets the color for the leave graph.
                /// </summary>
                public uint LeaveGraphColor { get; set; } = 4294956800;
            }
        }