using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Controllers;

/// <summary>
///     Reports which features are set up and switched on in a guild, for the dashboard's status dots and feature cards.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class FeatureStatusController(
    IDataConnectionFactory dbFactory,
    GuildSettingsService guildSettings,
    DiscordShardedClient client,
    DiscordAutoModService discordAutoMod,
    StickyService stickies,
    IMemoryCache cache,
    ILogger<FeatureStatusController> logger) : Controller
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(30);

    private static readonly PropertyInfo[] LogChannelProperties = typeof(LoggingV2)
        .GetProperties()
        .Where(x => x.PropertyType == typeof(ulong?))
        .ToArray();

    /// <summary>
    ///     Gets whether each feature is set up and whether it is switched on.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The status of every feature keyed by its dashboard slug.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId)
    {
        var response = await cache.GetOrCreateAsync($"featurestatus:{guildId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTime;
            return BuildAsync(guildId);
        });

        return Ok(response);
    }

    private async Task<FeatureStatusResponse> BuildAsync(ulong guildId)
    {
        GuildConfig? config = null;
        try
        {
            config = await guildSettings.GetGuildConfig(guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load the guild config for feature status in {GuildId}", guildId);
        }

        config ??= new GuildConfig();

        var lookups = CreateLookups();
        var gate = new SemaphoreSlim(8);
        var results = await Task.WhenAll(lookups.Select(async lookup =>
        {
            await gate.WaitAsync();
            try
            {
                await using var db = dbFactory.CreateConnection();
                return await lookup.Value(db, config, guildId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Feature status lookup for {Slug} failed in {GuildId}", lookup.Key, guildId);
                return new FeatureState(false, false);
            }
            finally
            {
                gate.Release();
            }
        }));

        var features = new Dictionary<string, FeatureState>();
        var index = 0;
        foreach (var slug in lookups.Keys)
            features[slug] = results[index++];

        return new FeatureStatusResponse(features);
    }

    private Dictionary<string, Func<MewdekoDb, GuildConfig, ulong, Task<FeatureState>>> CreateLookups()
    {
        return new Dictionary<string, Func<MewdekoDb, GuildConfig, ulong, Task<FeatureState>>>
        {
            ["administration"] = (_, c, _) => Task.FromResult(State(
                c.StaffRole != 0 || c.CommandLogChannel != 0 || c.DeleteMessageOnCommand || c.MemberRole != 0
                || !string.IsNullOrEmpty(c.AutoAssignRoleId) || !string.IsNullOrEmpty(c.AutoBotRoleIds)
                || !string.IsNullOrEmpty(c.MuteRoleName), true)),
            ["afk"] = async (db, c, g) =>
            {
                var any = await db.Afks.Where(x => x.GuildId == g).AnyAsync();
                return State(any || !string.IsNullOrEmpty(c.AfkMessage), true);
            },
            ["auditlog"] = (_, _, _) => Task.FromResult(State(false, true)),
            ["channel-access"] = async (db, _, g) =>
                Of(await Check(db.ChannelAccessConfigs.Where(x => x.GuildId == g), x => x.Enabled)),
            ["birthday"] = async (db, _, g) =>
                Of(await Check(db.BirthdayConfigs.Where(x => x.GuildId == g),
                    x => x.BirthdayChannelId != null && x.EnabledFeatures != 0)),
            ["wordoftheday"] = async (db, _, g) =>
                Of(await Check(db.WordOfTheDayConfigs.Where(x => x.GuildId == g),
                    x => x.Enabled && x.ChannelId != null)),
            ["chatsaver"] = async (db, _, g) =>
                State(await db.ChatLogs.Where(x => x.GuildId == g).AnyAsync(), true),
            ["access"] = async (db, _, g) =>
                State(await db.DashboardAccesses.Where(x => x.GuildId == g).AnyAsync(), true),
            ["confessions"] = (_, c, _) => Task.FromResult(State(
                c.ConfessionChannel != 0 || c.ConfessionLogChannel != 0 || !string.IsNullOrEmpty(c.ConfessionBlacklist),
                c.ConfessionChannel != 0)),
            ["counting"] = async (db, _, g) =>
                Of(await Check(db.CountingChannels.Where(x => x.GuildId == g), x => x.IsActive)),
            ["currency"] = async (db, c, g) =>
            {
                var config = await db.CurrencyConfigs.Where(x => x.GuildId == g).AnyAsync();
                var shop = await db.ShopItems.Where(x => x.GuildId == g).AnyAsync();
                return State(config || shop || !string.IsNullOrEmpty(c.CurrencyEmoji), true);
            },
            ["minecraft"] = async (db, _, g) =>
                Both(await db.MinecraftServers.Where(x => x.GuildId == g).AnyAsync()),
            ["statchannels"] = async (db, _, g) =>
                Both(await db.StatChannels.Where(x => x.GuildId == g).AnyAsync()),
            ["customvoice"] = async (db, _, g) =>
                Of(await Check(db.CustomVoiceConfigs.Where(x => x.GuildId == g), x => x.HubVoiceChannelId != 0)),
            ["embedbuilder"] = async (db, _, g) =>
                State(await db.Embeds.Where(x => x.GuildId == g).AnyAsync(), true),
            ["feeds"] = async (db, _, g) => Both(await db.FeedSubs.Where(x => x.GuildId == g).AnyAsync()),
            ["forms"] = async (db, _, g) =>
                Of(await Check(db.Forms.Where(x => x.GuildId == g), x => x.IsActive && !x.IsDraft)),
            ["giveaways"] = async (db, _, g) =>
                Of(await Check(db.Giveaways.Where(x => x.ServerId == g), x => x.Ended == 0)),
            ["multigreets"] = async (db, c, g) =>
            {
                var greets = await Check(db.MultiGreets.Where(x => x.GuildId == g), x => !x.Disabled);
                var dm = c.SendDmGreetMessage;
                var bye = c.SendChannelByeMessage && c.ByeMessageChannelId != 0;
                return State(greets.Any || dm || bye || c.SendBoostMessage, greets.On || dm || bye || c.SendBoostMessage);
            },
            ["highlights"] = async (db, _, g) =>
            {
                var any = await db.Highlights.Where(x => x.GuildId == g).AnyAsync();
                var on = any && await db.HighlightSettings.Where(x => x.GuildId == g && x.HighlightsOn).AnyAsync();
                return State(any, on);
            },
            ["invites"] = async (db, _, g) =>
                Of(await Check(db.InviteCountSettings.Where(x => x.GuildId == g), x => x.IsEnabled)),
            ["serverstats"] = async (db, _, g) =>
                State(await db.ServerStatsSettings.Where(x => x.GuildId == g).AnyAsync(), true),
            ["statroles"] = async (db, _, g) =>
                Of(await Check(db.StatRoles.Where(x => x.GuildId == g), x => x.Enabled)),
            ["linkfix"] = async (db, _, g) =>
                Of(await Check(db.LinkFixSettings.Where(x => x.GuildId == g), x => x.Enabled)),
            ["bridges"] = async (db, _, g) =>
                Of(await Check(db.ChannelBridges.Where(x => x.GuildId == g), x => !x.Paused)),
            ["liveboards"] = async (db, _, g) =>
            {
                var boards = await db.LiveBoards.Where(x => x.GuildId == g).AnyAsync();
                var reports = await Check(db.ServerReportSettings.Where(x => x.GuildId == g),
                    x => x.Enabled && x.ChannelId != null);
                return State(boards || reports.Any, boards || reports.On);
            },
            ["logging"] = async (db, _, g) =>
            {
                var row = await db.LoggingV2.Where(x => x.GuildId == g).FirstOrDefaultAsync();
                if (row == null)
                    return State(false, false);

                var on = LogChannelProperties.Any(x => x.GetValue(row) is ulong id && id != 0);
                return State(true, on);
            },
            ["messagestats"] = (_, c, _) => Task.FromResult(State(c.UseMessageCount, c.UseMessageCount)),
            ["filter"] = async (db, c, g) =>
            {
                var words = await db.FilteredWords.Where(x => x.GuildId == g).AnyAsync();
                var on = c.FilterInvites || c.FilterLinks || c.FilterWords;
                return State(on || words, on);
            },
            ["polls"] = async (db, _, g) =>
                State(await db.Polls.Where(x => x.GuildId == g).AnyAsync(), true),
            ["utility"] = (_, _, _) => Task.FromResult(State(false, true)),
            ["moderation"] = async (db, c, g) =>
            {
                var punishments = await db.WarningPunishments.Where(x => x.GuildId == g).AnyAsync();
                var bans = await db.AutoBanWords.Where(x => x.GuildId == g).AnyAsync();
                return State(
                    punishments || bans || c.WarnlogChannelId != 0 || c.MiniWarnlogChannelId != 0
                    || c.WarnExpireHours != 0 || !string.IsNullOrEmpty(c.MuteRoleName), true);
            },
            ["mod-cases"] = async (db, _, g) =>
            {
                var settings = await Check(db.ModCaseSettings.Where(x => x.GuildId == g), x => x.LogChannelId != null);
                var cases = await db.ModCases.Where(x => x.GuildId == g).AnyAsync();
                return State(settings.Any || cases, settings.On);
            },
            ["music"] = async (db, _, g) =>
                State(await db.MusicPlayerSettings.Where(x => x.GuildId == g).AnyAsync(), true),
            ["patreon"] = (_, c, _) =>
            {
                var on = c.PatreonEnabled && !string.IsNullOrEmpty(c.PatreonCampaignId);
                return Task.FromResult(State(on || !string.IsNullOrEmpty(c.PatreonCampaignId) || c.PatreonChannelId != 0,
                    on));
            },
            ["feature-requests"] = (_, _, _) => Task.FromResult(State(false, true)),
            ["repeaters"] = async (db, _, g) =>
                Of(await Check(db.GuildRepeaters.Where(x => x.GuildId == g), x => x.IsEnabled)),
            ["achievements"] = async (db, _, g) =>
                Of(await Check(db.AchievementSettings.Where(x => x.GuildId == g), x => x.Enabled)),
            ["reputation"] = async (db, _, g) =>
                Of(await Check(db.RepConfigs.Where(x => x.GuildId == g), x => x.Enabled)),
            ["rolegreets"] = async (db, _, g) =>
                Of(await Check(db.RoleGreets.Where(x => x.GuildId == g), x => !x.Disabled)),
            ["role-menus"] = async (db, _, g) =>
                Of(await Check(db.RoleMenus.Where(x => x.GuildId == g), x => x.Enabled && x.MessageId != null)),
            ["rolestates"] = async (db, _, g) =>
                Of(await Check(db.RoleStateSettings.Where(x => x.GuildId == g), x => x.Enabled)),
            ["settings"] = (_, _, _) => Task.FromResult(State(false, true)),
            ["starboard"] = async (db, _, g) =>
                Of(await Check(db.Starboards.Where(x => x.GuildId == g),
                    x => x.Enabled && x.StarboardChannelId != 0)),
            ["statusroles"] = async (db, _, g) =>
                Of(await Check(db.StatusRoles.Where(x => x.GuildId == g), x => x.Status != null && x.Status != "")),
            ["streams"] = async (db, _, g) =>
            {
                var followed = await db.FollowedStreams.Where(x => x.GuildId == g).AnyAsync();
                var roles = await Check(db.StreamRoleSettings.Where(x => x.GuildId == g), x => x.Enabled);
                return State(followed || roles.Any, followed || roles.On);
            },
            ["twitch"] = async (db, _, g) =>
                Of(await Check(db.TwitchGuildConfigs.Where(x => x.GuildId == g), x => x.Enabled)),
            ["suggestions"] = (_, c, _) => Task.FromResult(State(
                c.Sugchan != 0 || c.SuggestButtonChannel != 0 || c.AcceptChannel != 0 || c.DenyChannel != 0
                || c.ConsiderChannel != 0 || c.ImplementChannel != 0, c.Sugchan != 0)),
            ["tickets"] = async (db, _, g) =>
            {
                var panels = await db.TicketPanels.Where(x => x.GuildId == g).AnyAsync();
                var settings = await db.GuildTicketSettings.Where(x => x.GuildId == g).AnyAsync();
                return State(panels || settings, panels);
            },
            ["todo"] = async (db, _, g) =>
                State(await db.TodoLists.Where(x => x.GuildId == g).AnyAsync(), true),
            ["chat-triggers"] = async (db, _, g) =>
                Of(await Check(db.ChatTriggers.Where(x => x.GuildId == g), x => !x.IsDisabled)),
            ["votes"] = async (db, c, g) =>
            {
                var roles = await db.VoteRoles.Where(x => x.GuildId == g).AnyAsync();
                return State(c.VotesChannel != 0 || roles || !string.IsNullOrEmpty(c.VotesPassword),
                    c.VotesChannel != 0);
            },
            ["xp"] = async (db, _, g) =>
                Of(await Check(db.GuildXpSettings.Where(x => x.GuildId == g), x => !x.XpGainDisabled)),
            ["import"] = async (db, _, g) =>
                State(await db.DataImports.Where(x => x.GuildId == g).AnyAsync(), true),
            ["automations"] = async (db, _, g) =>
                Of(await Check(db.Automations.Where(x => x.GuildId == g), x => x.Enabled)),
            ["verification"] = async (db, _, g) =>
                Of(await Check(db.VerificationSettings.Where(x => x.GuildId == g),
                    x => x.Enabled && x.ChannelId != null)),
            ["welcome-cards"] = async (db, _, g) =>
                Of(await Check(db.WelcomeCardSettings.Where(x => x.GuildId == g), x => x.Enabled)),
            ["automod"] = async (db, _, g) =>
            {
                var rules = await Check(db.AutomodRuleSettings.Where(x => x.GuildId == g), x => x.Enabled);
                var any = rules.Any;
                var on = rules.On;
                foreach (var found in new[]
                         {
                             await db.AntiSpamSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiMassMentionSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiMassPostSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiExternalAppSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiImageHashSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiPostChannelSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiRaidSettings.Where(x => x.GuildId == g).AnyAsync(),
                             await db.AntiAltSettings.Where(x => x.GuildId == g).AnyAsync()
                         })
                {
                    any |= found;
                    on |= found;
                }

                return State(any, on);
            },
            ["discord-automod"] = async (_, _, g) =>
            {
                var guild = client.GetGuild(g);
                if (guild == null)
                    return State(false, false);

                var (failure, rules) = await discordAutoMod.ListAsync(guild);
                return failure != null
                    ? State(false, false)
                    : State(rules.Count > 0, rules.Any(x => x.Enabled));
            },
            ["auto-reactions"] = async (db, _, g) =>
                Of(await Check(db.AutoReactions.Where(x => x.GuildId == g), x => x.Enabled)),
            ["stickies"] = async (db, _, g) =>
            {
                var any = stickies.GetStickies(g).Count > 0;
                var templates = await db.StickyTemplates.Where(x => x.GuildId == g).AnyAsync();
                return State(any || templates, any);
            },
            ["backups"] = async (db, _, g) =>
                State(await db.ServerBackups.Where(x => x.SourceGuildId == g).AnyAsync(), true),
            ["templates"] = (_, _, _) => Task.FromResult(State(false, true)),
            ["channel-tools"] = async (db, _, g) =>
            {
                var purge = await db.AutoPurgeSettings.Where(x => x.GuildId == g).AnyAsync();
                var timed = await db.ChannelTimedActions.Where(x => x.GuildId == g).AnyAsync();
                var links = await db.VoiceTextLinks.Where(x => x.GuildId == g).AnyAsync();
                return Both(purge || timed || links);
            },
            ["invite-roles"] = async (db, _, g) =>
            {
                var roles = await db.InviteRoles.Where(x => x.GuildId == g).AnyAsync();
                var settings = await db.InviteRoleSettings.Where(x => x.GuildId == g).AnyAsync();
                return State(roles || settings, roles);
            }
        };
    }

    private static async Task<Probe> Check<T>(IQueryable<T> rows, Expression<Func<T, bool>> on)
    {
        var flags = await rows.Select(on).Distinct().ToListAsync();
        return new Probe(flags.Count > 0, flags.Contains(true));
    }

    private static FeatureState State(bool configured, bool enabled)
    {
        return new FeatureState(enabled, configured || enabled);
    }

    private static FeatureState Of(Probe probe)
    {
        return State(probe.Any, probe.On);
    }

    private static FeatureState Both(bool value)
    {
        return State(value, value);
    }

    private readonly record struct Probe(bool Any, bool On);

    /// <summary>
    ///     The status of one feature.
    /// </summary>
    /// <param name="Enabled">Whether the feature is active.</param>
    /// <param name="Configured">Whether the guild has set anything up for the feature.</param>
    public sealed record FeatureState(
        [property: JsonPropertyName("enabled")]
        bool Enabled,
        [property: JsonPropertyName("configured")]
        bool Configured);

    /// <summary>
    ///     The status of every feature keyed by its dashboard slug.
    /// </summary>
    /// <param name="Features">The feature statuses.</param>
    public sealed record FeatureStatusResponse(
        [property: JsonPropertyName("features")]
        Dictionary<string, FeatureState> Features);
}
