using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.OwnerOnly;

[SlashOwnerOnly, Discord.Interactions.Group("owneronly", "Commands only the bot owner can use")]
public class SlashOwnerOnly : MewdekoSlashModuleBase<OwnerOnlyService>
{
    public enum SettableUserStatus
    {
        Online,
        Invisible,
        Idle,
        Dnd
    }

    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly ICoordinator coord;
    private readonly IBotStrings strings;
    private readonly InteractiveService interactivity;
    private readonly IDataCache cache;
    private readonly GuildSettingsService guildSettings;
    private readonly CommandHandler commandHandler;

    public SlashOwnerOnly(
        DiscordSocketClient client,
        IBotStrings strings,
        InteractiveService serv,
        ICoordinator coord,
        DbService db,
        IDataCache cache,
        GuildSettingsService guildSettings,
        CommandHandler commandHandler)
    {
        interactivity = serv;
        this.client = client;
        this.strings = strings;
        this.coord = coord;
        this.db = db;
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.commandHandler = commandHandler;
    }

    [SlashCommand("sudo", "Run a command as another user")]
    public async Task Sudo([Remainder] string args, IUser user = null)
    {
        user ??= await Context.Guild.GetOwnerAsync();
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{args}", Author = user, Channel = ctx.Channel
        };
        commandHandler.AddCommandToParseQueue(msg);
        _ = Task.Run(async () => await commandHandler.ExecuteCommandsInChannelAsync(ctx.Interaction.Id)).ConfigureAwait(false);
    }

    [SlashCommand("redisexec", "Run a redis command")]
    public async Task RedisExec([Remainder] string command)
    {
        var result = await cache.ExecuteRedisCommand(command).ConfigureAwait(false);
        var eb = new EmbedBuilder().WithOkColor().WithTitle(result.Type.ToString()).WithDescription(result.ToString());
        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [SlashCommand("sqlexec", "Run a sql command")]
    public async Task SqlExec([Remainder] string sql)
    {
        if (!await PromptUserConfirmAsync("Are you sure you want to execute this??", ctx.User.Id).ConfigureAwait(false))
            return;
        await using var uow = db.GetDbContext();
        var affected = await uow.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
        await ctx.Interaction.SendErrorAsync($"Affected {affected} rows.").ConfigureAwait(false);
    }

    [SlashCommand("listservers", "List all servers the bot is in")]
    public async Task ListServers()
    {
        var guilds = client.Guilds;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(guilds.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            var newGuilds = guilds.Skip(10 * page);
            var eb = new PageBuilder().WithOkColor().WithTitle("Servers List");
            foreach (var i in newGuilds)
            {
                eb.AddField($"{i.Name} | {i.Id}", $"Members: {i.Users.Count}"
                                                  + $"\nOnline Members: {i.Users.Count(x => x.Status is UserStatus.Online or UserStatus.DoNotDisturb or UserStatus.Idle)}"
                                                  + $"\nOwner: {i.Owner} | {i.OwnerId}"
                                                  + $"\n Created On: {TimestampTag.FromDateTimeOffset(i.CreatedAt)}");
            }

            return eb;
        }
    }

    [SlashCommand("commandstats", "Get stats about commands")]
    public async Task CommandStats()
    {
        await using var uow = db.GetDbContext();
        var commandStatsTable = uow.CommandStats;
        // fetch actual tops
        var topCommand = await commandStatsTable.Where(x => !x.Trigger).GroupBy(q => q.NameOrId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topModule = await commandStatsTable.Where(x => !x.Trigger).GroupBy(q => q.Module)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topGuild = await commandStatsTable.Where(x => !x.Trigger).GroupBy(q => q.GuildId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();
        var topUser = await commandStatsTable.Where(x => !x.Trigger).GroupBy(q => q.UserId)
            .OrderByDescending(gp => gp.Count()).Select(x => x.Key).FirstOrDefaultAsyncLinqToDB();

        // then fetch their counts... This can probably be done better....
        var topCommandCount = commandStatsTable.Count(x => x.NameOrId == topCommand);
        var topModuleCount = commandStatsTable.Count(x => x.NameOrId == topCommand);
        var topGuildCount = commandStatsTable.Count(x => x.GuildId == topGuild);
        var topUserCount = commandStatsTable.Count(x => x.UserId == topUser);

        var guild = await client.Rest.GetGuildAsync(topGuild);
        var user = await client.Rest.GetUserAsync(topUser);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField("Top Command", $"{topCommand} was used {topCommandCount} times!")
            .AddField("Top Module", $"{topModule} was used {topModuleCount} times!")
            .AddField("Top User", $"{user} has used commands {topUserCount} times!")
            .AddField("Top Guild", $"{guild} has used commands {topGuildCount} times!");

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    [Discord.Interactions.Group("config", "Commands to manage various bot things")]
    public class ConfigCommands : MewdekoSlashModuleBase<OwnerOnlyService>
    {
        private readonly GuildSettingsService guildSettings;
        private readonly CommandService commandService;
        private readonly IServiceProvider services;
        private readonly DiscordSocketClient client;
        private readonly IEnumerable<IConfigService> settingServices;

        public ConfigCommands(GuildSettingsService guildSettings, CommandService commandService, IServiceProvider services, DiscordSocketClient client,
            IEnumerable<IConfigService> settingServices)
        {
            this.guildSettings = guildSettings;
            this.commandService = commandService;
            this.services = services;
            this.client = client;
            this.settingServices = settingServices;
        }

        [SlashCommand("defprefix", "Sets the default prefix for the bots text commands")]
        public async Task DefPrefix(string? prefix = null)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                await ReplyConfirmLocalizedAsync("defprefix_current", await guildSettings.GetPrefix()).ConfigureAwait(false);
                return;
            }

            var oldPrefix = await guildSettings.GetPrefix();
            var newPrefix = Service.SetDefaultPrefix(prefix);

            await ReplyConfirmLocalizedAsync("defprefix_new", Format.Code(oldPrefix), Format.Code(newPrefix))
                .ConfigureAwait(false);
        }

        [SlashCommand("langsetdefault", "Sets the default language for the bot")]
        public async Task LanguageSetDefault(string name)
        {
            try
            {
                CultureInfo? ci;
                if (string.Equals(name.Trim(), "default", StringComparison.InvariantCultureIgnoreCase))
                {
                    Localization.ResetDefaultCulture();
                    ci = Localization.DefaultCultureInfo;
                }
                else
                {
                    ci = new CultureInfo(name);
                    Localization.SetDefaultCulture(ci);
                }

                await ReplyConfirmLocalizedAsync("lang_set_bot", Format.Bold(ci.ToString()),
                    Format.Bold(ci.NativeName)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await ReplyErrorLocalizedAsync("lang_set_fail").ConfigureAwait(false);
            }
        }

        [SlashCommand("startupcommandadd", "Adds a command to run in the current channel on startup"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild),
         SlashUserPerm(GuildPermission.Administrator)]
        public async Task StartupCommandAdd([Remainder] string cmdText)
        {
            if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die", StringComparison.InvariantCulture) ||
                cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}restart", StringComparison.InvariantCulture))
                return;

            var guser = (IGuildUser)ctx.User;
            var cmd = new AutoCommand
            {
                CommandText = cmdText,
                ChannelId = ctx.Interaction.Id,
                ChannelName = ctx.Channel.Name,
                GuildId = ctx.Guild?.Id,
                GuildName = ctx.Guild?.Name,
                VoiceChannelId = guser.VoiceChannel?.Id,
                VoiceChannelName = guser.VoiceChannel?.Name,
                Interval = 0
            };
            Service.AddNewAutoCommand(cmd);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("scadd"))
                .AddField(efb => efb.WithName(GetText("server"))
                    .WithValue(cmd.GuildId == null ? "-" : $"{cmd.GuildName}/{cmd.GuildId}").WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("channel"))
                    .WithValue($"{cmd.ChannelName}/{cmd.ChannelId}").WithIsInline(true))
                .AddField(efb => efb.WithName(GetText("command_text"))
                    .WithValue(cmdText).WithIsInline(false)).Build()).ConfigureAwait(false);
        }

        [SlashCommand("autocommandadd", "Adds a command to run at a set interval"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild),
         SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoCommandAdd(int interval, [Remainder] string cmdText)
        {
            if (cmdText.StartsWith($"{await guildSettings.GetPrefix(ctx.Guild)}die", StringComparison.InvariantCulture))
                return;
            var command = commandService.Search(cmdText.Replace(await guildSettings.GetPrefix(ctx.Guild), "").Split(" ")[0]);
            if (!command.IsSuccess)
                return;

            var currentContext = new CommandContext(ctx.Client as DiscordSocketClient, new MewdekoUserMessage
            {
                Content = "HI!", Author = ctx.User, Channel = ctx.Channel
            });

            foreach (var i in command.Commands)
            {
                if (!(await i.CheckPreconditionsAsync(currentContext, services).ConfigureAwait(false)).IsSuccess)
                    return;
            }

            var count = Service.GetAutoCommands().Where(x => x.GuildId == ctx.Guild.Id);

            if (count.Count() == 15)
                return;
            if (interval < 5)
                return;

            var guser = (IGuildUser)ctx.User;
            var cmd = new AutoCommand
            {
                CommandText = cmdText,
                ChannelId = ctx.Interaction.Id,
                ChannelName = ctx.Channel.Name,
                GuildId = ctx.Guild?.Id,
                GuildName = ctx.Guild?.Name,
                VoiceChannelId = guser.VoiceChannel?.Id,
                VoiceChannelName = guser.VoiceChannel?.Name,
                Interval = interval
            };
            Service.AddNewAutoCommand(cmd);

            await ReplyConfirmLocalizedAsync("autocmd_add", Format.Code(Format.Sanitize(cmdText)), cmd.Interval)
                .ConfigureAwait(false);
        }

        [SlashCommand("startupcommandslist", "Lists the current startup commands"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild)]
        public async Task StartupCommandsList(int page = 1)
        {
            if (page-- < 1)
                return;

            var scmds = Service.GetStartupCommands()
                .Skip(page * 5)
                .Take(5)
                .ToList();

            if (scmds.Count == 0)
            {
                await ReplyErrorLocalizedAsync("startcmdlist_none").ConfigureAwait(false);
            }
            else
            {
                var i = 0;
                await ctx.Interaction.SendConfirmAsync(
                        text: string.Join("\n", scmds
                            .Select(x => $@"```css
#{++i}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
[{GetText("command_text")}]: {x.CommandText}```")),
                        title: string.Empty,
                        footer: GetText("page", page + 1))
                    .ConfigureAwait(false);
            }
        }

        [SlashCommand("autocommandslist", "Lists all auto commands"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild)]
        public async Task AutoCommandsList(int page = 1)
        {
            if (page-- < 1)
                return;

            var scmds = Service.GetAutoCommands()
                .Skip(page * 5)
                .Take(5)
                .ToList();
            if (scmds.Count == 0)
            {
                await ReplyErrorLocalizedAsync("autocmdlist_none").ConfigureAwait(false);
            }
            else
            {
                var i = 0;
                await ctx.Interaction.SendConfirmAsync(
                        text: string.Join("\n", scmds
                            .Select(x => $@"```css
#{++i}
[{GetText("server")}]: {(x.GuildId.HasValue ? $"{x.GuildName} #{x.GuildId}" : "-")}
[{GetText("channel")}]: {x.ChannelName} #{x.ChannelId}
{GetIntervalText(x.Interval)}
[{GetText("command_text")}]: {x.CommandText}```")),
                        title: string.Empty,
                        footer: GetText("page", page + 1))
                    .ConfigureAwait(false);
            }
        }

        private string GetIntervalText(int interval) => $"[{GetText("interval")}]: {interval}";

        [SlashCommand("autocommandremove", "Removes an auto command"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild),
         SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoCommandRemove([Remainder] int index)
        {
            if (!Service.RemoveAutoCommand(--index, out _))
            {
                await ReplyErrorLocalizedAsync("acrm_fail").ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendConfirmAsync($"Auto Command Removed.").ConfigureAwait(false);
        }

        [SlashCommand("startupcommandremove", "Removes a startup command"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild)]
        public async Task StartupCommandRemove([Remainder] int index)
        {
            if (!Service.RemoveStartupCommand(--index, out _))
                await ReplyErrorLocalizedAsync("scrm_fail").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("scrm").ConfigureAwait(false);
        }

        [SlashCommand("startupcommandsclear", "Clears all startup commands"), Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild),
         SlashUserPerm(GuildPermission.Administrator)]
        public async Task StartupCommandsClear()
        {
            Service.ClearStartupCommands();

            await ReplyConfirmLocalizedAsync("startcmds_cleared").ConfigureAwait(false);
        }

        [SlashCommand("forwardmessages", "Toggles whether to forward dms to the bot to owner dms")]
        public async Task ForwardMessages()
        {
            var enabled = Service.ForwardMessages();

            if (enabled)
                await ReplyConfirmLocalizedAsync("fwdm_start").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("fwdm_stop").ConfigureAwait(false);
        }

        [SlashCommand("forwardtoall", "Toggles whether to forward dms to the bot to all bot owners")]
        public async Task ForwardToAll()
        {
            var enabled = Service.ForwardToAll();

            if (enabled)
                await ReplyConfirmLocalizedAsync("fwall_start").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("fwall_stop").ConfigureAwait(false);
        }

        [SlashCommand("setname", "Sets the bots name")]
        public async Task SetName([Remainder] string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return;

            try
            {
                await client.CurrentUser.ModifyAsync(u => u.Username = newName).ConfigureAwait(false);
            }
            catch (RateLimitedException)
            {
                Log.Warning("You've been ratelimited. Wait 2 hours to change your name");
            }

            await ReplyConfirmLocalizedAsync("bot_name", Format.Bold(newName)).ConfigureAwait(false);
        }


        [SlashCommand("setavatar", "Sets the bots avatar")]
        public async Task SetAvatar([Remainder] string? img = null)
        {
            var success = await Service.SetAvatar(img).ConfigureAwait(false);

            if (success)
                await ReplyConfirmLocalizedAsync("set_avatar").ConfigureAwait(false);
        }

        [SlashCommand("botconfig", "Config various bot settings")]
        public async Task Config([Autocomplete(typeof(SettingsServiceNameAutoCompleter))] string? name = null,
            [Autocomplete(typeof(SettingsServicePropAutoCompleter))]
            string? prop = null, [Remainder] string? value = null)
        {
            await DeferAsync();
            try
            {
                var configNames = settingServices.Select(x => x.Name);

                // if name is not provided, print available configs
                name = name?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name))
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("config_list"))
                        .WithDescription(string.Join("\n", configNames));

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                var setting = settingServices.FirstOrDefault(x =>
                    x.Name.StartsWith(name, StringComparison.InvariantCultureIgnoreCase));

                // if config name is not found, print error and the list of configs
                if (setting is null)
                {
                    var embed = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("config_not_found", Format.Code(name)))
                        .AddField(GetText("config_list"), string.Join("\n", configNames));

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                name = setting.Name;

                // if prop is not sent, then print the list of all props and values in that config
                prop = prop?.ToLowerInvariant();
                var propNames = setting.GetSettableProps();
                if (string.IsNullOrWhiteSpace(prop))
                {
                    var propStrings = GetPropsAndValuesString(setting, propNames);
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"⚙️ {setting.Name}")
                        .WithDescription(propStrings);

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }
                // if the prop is invalid -> print error and list of

                var exists = propNames.Any(x => x == prop);

                if (!exists)
                {
                    var propStrings = GetPropsAndValuesString(setting, propNames);
                    var propErrorEmbed = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("config_prop_not_found", Format.Code(prop), Format.Code(name)))
                        .AddField($"⚙️ {setting.Name}", propStrings);

                    await ctx.Interaction.FollowupAsync(embed: propErrorEmbed.Build()).ConfigureAwait(false);
                    return;
                }

                // if prop is sent, but value is not, then we have to check
                // if prop is valid ->
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = setting.GetSetting(prop);
                    if (prop != "currency.sign")
                        Format.Code(Format.Sanitize(value.TrimTo(1000)), "json");

                    if (string.IsNullOrWhiteSpace(value))
                        value = "-";

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .AddField("Config", Format.Code(setting.Name), true)
                        .AddField("Prop", Format.Code(prop), true)
                        .AddField("Value", value);

                    var comment = setting.GetComment(prop);
                    if (!string.IsNullOrWhiteSpace(comment))
                        embed.AddField("Comment", comment);

                    await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }

                var success = setting.SetSetting(prop, value);

                if (!success)
                {
                    await ReplyErrorLocalizedAsync("config_edit_fail", Format.Code(prop), Format.Code(value)).ConfigureAwait(false);
                    return;
                }

                await ctx.Interaction.SendConfirmFollowupAsync("Config updated!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await ctx.Interaction.SendErrorFollowupAsync("There was an error setting or printing the config, please check the logs.");
            }
        }

        private static string GetPropsAndValuesString(IConfigService config, IEnumerable<string> names)
        {
            var propValues = names.Select(pr =>
            {
                var val = config.GetSetting(pr);
                if (pr != "currency.sign")
                    val = val.TrimTo(40);
                return val?.Replace("\n", "") ?? "-";
            });

            var strings = names.Zip(propValues, (name, value) =>
                $"{name,-25} = {value}\n");

            return string.Concat(strings);
        }
    }

    [Discord.Interactions.Group("statuscommands", "Commands to manage bot status")]
    public class StatusCommands : MewdekoSlashModuleBase<OwnerOnlyService>
    {
        private readonly Mewdeko bot;
        private readonly DiscordSocketClient client;

        public StatusCommands(Mewdeko bot, DiscordSocketClient client)
        {
            this.bot = bot;
            this.client = client;
        }

        [SlashCommand("rotateplaying", "Toggles rotating playing status")]
        public async Task RotatePlaying()
        {
            if (Service.ToggleRotatePlaying())
                await ReplyConfirmLocalizedAsync("ropl_enabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("ropl_disabled").ConfigureAwait(false);
        }

        [SlashCommand("addplaying", "Adds a playing status to the rotating status list")]
        public async Task AddPlaying(ActivityType t, [Remainder] string status)
        {
            await Service.AddPlaying(t, status).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("ropl_added").ConfigureAwait(false);
        }

        [SlashCommand("listplaying", "Lists all rotating statuses")]
        public async Task ListPlaying()
        {
            var statuses = Service.GetRotatingStatuses();

            if (statuses.Count == 0)
            {
                await ReplyErrorLocalizedAsync("ropl_not_set").ConfigureAwait(false);
            }
            else
            {
                var i = 1;
                await ReplyConfirmLocalizedAsync("ropl_list",
                        string.Join("\n\t", statuses.Select(rs => $"`{i++}.` *{rs.Type}* {rs.Status}")))
                    .ConfigureAwait(false);
            }
        }

        [SlashCommand("removeplaying", "Removes a status from the rotating status list")]
        public async Task RemovePlaying(int index)
        {
            index--;

            var msg = await Service.RemovePlayingAsync(index).ConfigureAwait(false);

            if (msg == null)
                return;

            await ReplyConfirmLocalizedAsync("reprm", msg).ConfigureAwait(false);
        }

        [SlashCommand("setstatus", "Sets the bots status (DND, Offline, etc)")]
        public async Task SetStatus([Remainder] SettableUserStatus status)
        {
            await client.SetStatusAsync(SettableUserStatusToUserStatus(status)).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("bot_status", Format.Bold(status.ToString())).ConfigureAwait(false);
        }

        [SlashCommand("setgame", "Sets the bots now playing. Disabled rotating status")]
        public async Task SetGame(ActivityType type, [Remainder] string? game = null)
        {
            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            await bot.SetGameAsync(game == null ? game : rep.Replace(game), type).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("set_game").ConfigureAwait(false);
        }

        [SlashCommand("setstream", "Sets the stream url (such as Twitch)")]
        public async Task SetStream(string url, [Remainder] string? name = null)
        {
            name ??= "";

            await client.SetGameAsync(name, url, ActivityType.Streaming).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("set_stream").ConfigureAwait(false);
        }
    }


    [SlashCommand("shardstats", "Shows the stats for all shards")]
    public async Task ShardStats()
    {
        var statuses = coord.GetAllShardStatuses();

        var status = string.Join(" : ", statuses
            .Select(x => (ConnectionStateToEmoji(x), x))
            .GroupBy(x => x.Item1)
            .Select(x => $"`{x.Count()} {x.Key}`")
            .ToArray());

        var allShardStrings = statuses
            .Select(st =>
            {
                var stateStr = ConnectionStateToEmoji(st);
                var timeDiff = DateTime.UtcNow - st.LastUpdate;
                var maxGuildCountLength = statuses.Max(x => x.GuildCount).ToString().Length;
                return
                    $"`{stateStr} | #{st.ShardId.ToString().PadBoth(3)} | {timeDiff:mm\\:ss} | {st.GuildCount.ToString().PadBoth(maxGuildCountLength)} | {st.UserCount}`";
            })
            .ToArray();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(allShardStrings.Length / 25)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var str = string.Join("\n", allShardStrings.Skip(25 * page).Take(25));

            if (string.IsNullOrWhiteSpace(str))
                str = GetText("no_shards_on_page");

            return new PageBuilder()
                .WithAuthor(a => a.WithName(GetText("shard_stats")))
                .WithTitle(status)
                .WithColor(Mewdeko.OkColor)
                .WithDescription(str);
        }
    }

    private static string ConnectionStateToEmoji(ShardStatus status)
    {
        var timeDiff = DateTime.UtcNow - status.LastUpdate;
        return status.ConnectionState switch
        {
            ConnectionState.Connected => "✅",
            ConnectionState.Disconnected => "🔻",
            _ when timeDiff > TimeSpan.FromSeconds(30) => " ❗ ",
            _ => " ⏳"
        };
    }

    [SlashCommand("restartshard", "Restarts a shard by its number")]
    public async Task RestartShard(int shardId)
    {
        var success = coord.RestartShard(shardId);
        if (success)
            await ReplyConfirmLocalizedAsync("shard_reconnecting", Format.Bold($"#{shardId}")).ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("no_shard_id").ConfigureAwait(false);
    }

    [SlashCommand("leaveserver", "Leaves a server by id or name")]
    public Task LeaveServer([Remainder] string guildStr) => Service.LeaveGuild(guildStr);

    [SlashCommand("die", "Shuts down the bot")]
    public async Task Die()
    {
        try
        {
            await ReplyConfirmLocalizedAsync("shutting_down").ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await Task.Delay(2000).ConfigureAwait(false);
        Environment.SetEnvironmentVariable("SNIPE_CACHED", "0");
        Environment.SetEnvironmentVariable("AFK_CACHED", "0");
        coord.Die();
    }

    [SlashCommand("restart", "Restarts the bot, restart command must be set in credentials")]
    public async Task Restart()
    {
        var success = coord.RestartBot();
        if (!success)
        {
            await ReplyErrorLocalizedAsync("restart_fail").ConfigureAwait(false);
            return;
        }

        try
        {
            await ReplyConfirmLocalizedAsync("restarting").ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }


    [SlashCommand("send", "Sends a message to a server or dm")]
    public async Task Send(ulong whereOrTo, ulong to = 0, [Remainder] string? msg = null)
    {
        var rep = new ReplacementBuilder().WithDefault(Context).Build();
        RestGuild potentialServer;
        try
        {
            potentialServer = await client.Rest.GetGuildAsync(whereOrTo).ConfigureAwait(false);
        }
        catch
        {
            var potentialUser = client.GetUser(whereOrTo);
            if (potentialUser is null)
            {
                await ctx.Interaction.SendErrorAsync("Unable to find that user or guild! Please double check the Id!").ConfigureAwait(false);
                return;
            }

            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed, out var plainText, out var components))
            {
                await potentialUser.SendMessageAsync(plainText, embeds: embed, components: components.Build()).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialUser.Mention}!").ConfigureAwait(false);
                return;
            }

            await potentialUser.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialUser.Mention}!").ConfigureAwait(false);
            return;
        }

        if (to == 0)
        {
            await ctx.Interaction.SendErrorAsync("You need to specify a Channel or User ID after the Server ID!").ConfigureAwait(false);
            return;
        }

        var channel = await potentialServer.GetTextChannelAsync(to).ConfigureAwait(false);
        if (channel is not null)
        {
            if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild.Id, out var embed, out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build()).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}").ConfigureAwait(false);
                return;
            }

            await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {channel.Mention}").ConfigureAwait(false);
            return;
        }

        var user = await potentialServer.GetUserAsync(to).ConfigureAwait(false);
        if (user is null)
        {
            await ctx.Interaction.SendErrorAsync("Unable to find that channel or user! Please check the ID and try again.").ConfigureAwait(false);
            return;
        }

        if (SmartEmbed.TryParse(rep.Replace(msg), ctx.Guild?.Id, out var embed1, out var plainText1, out var components1))
        {
            await channel.SendMessageAsync(plainText1, embeds: embed1, components: components1?.Build()).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} to {user.Mention}").ConfigureAwait(false);
            return;
        }

        await channel.SendMessageAsync(rep.Replace(msg)).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Message sent to {potentialServer} in {user.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("imagesreload", "Recaches and redownloads all images")]
    public async Task ImagesReload()
    {
        Service.ReloadImages();
        await ReplyConfirmLocalizedAsync("images_loading", 0).ConfigureAwait(false);
    }

    [SlashCommand("stringsreload", "Reloads localized strings")]
    public async Task StringsReload()
    {
        strings.Reload();
        await ReplyConfirmLocalizedAsync("bot_strings_reloaded").ConfigureAwait(false);
    }

    private static UserStatus SettableUserStatusToUserStatus(SettableUserStatus sus) =>
        sus switch
        {
            SettableUserStatus.Online => UserStatus.Online,
            SettableUserStatus.Invisible => UserStatus.Invisible,
            SettableUserStatus.Idle => UserStatus.AFK,
            SettableUserStatus.Dnd => UserStatus.DoNotDisturb,
            _ => UserStatus.Online
        };

    [SlashCommand("bash", "Executes a bash command on the host machine")]
    public async Task Bash([Remainder] string message)
    {
        await DeferAsync();
        using var process = new Process();
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (isLinux)
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{message} 2>&1\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"/c \"{message} 2>&1\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        using (ctx.Channel.EnterTypingState())
        {
            process.Start();
            var reader = process.StandardOutput;
            var timeout = TimeSpan.FromHours(2);
            var task = Task.Run(() => reader.ReadToEndAsync());
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                var output = await task.ConfigureAwait(false);

                if (string.IsNullOrEmpty(output))
                {
                    await ctx.Interaction.FollowupAsync("```The output was blank```").ConfigureAwait(false);
                    return;
                }

                var chunkSize = 1988;
                var stringList = new List<string>();

                for (var i = 0; i < output.Length; i += chunkSize)
                {
                    if (i + chunkSize > output.Length)
                        chunkSize = output.Length - i;
                    stringList.Add(output.Substring(i, chunkSize));

                    if (stringList.Count < 50) continue;
                    process.Kill();
                    break;
                }

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(stringList.Count - 1)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;

                    return new PageBuilder()
                        .WithOkColor()
                        .WithAuthor($"Bash Output")
                        .AddField("Input", message)
                        .WithDescription($"```{(isLinux ? "bash" : "powershell")}\n{stringList[page]}```");
                }
            }
            else
            {
                process.Kill();
                await ctx.Interaction.FollowupAsync("The process was hanging and has been terminated.").ConfigureAwait(false);
            }

            if (!process.HasExited)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    [SlashCommand("eval", "Eval C# code"), OwnerOnly]
    public async Task Evaluate()
        => await ctx.Interaction.RespondWithModalAsync<EvalModal>("evalhandle");

    [ModalInteraction("evalhandle", true)]
    public async Task EvaluateModalInteraction(EvalModal modal)
    {
        await DeferAsync();

        var embed = new EmbedBuilder
        {
            Title = "Evaluating...", Color = new Color(0xD091B2)
        };
        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);

        var globals = new InteractionEvaluationEnvironment(ctx);
        var sopts = ScriptOptions.Default
            .WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
                "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Text",
                "System.Threading.Tasks", "Discord.Net", "Discord", "Discord.WebSocket", "Mewdeko.Modules",
                "Mewdeko.Services", "Mewdeko.Extensions", "Mewdeko.Modules.Administration",
                "Mewdeko.Modules.Chat_Triggers", "Mewdeko.Modules.Gambling", "Mewdeko.Modules.Games",
                "Mewdeko.Modules.Help", "Mewdeko.Modules.Music", "Mewdeko.Modules.Nsfw",
                "Mewdeko.Modules.Permissions", "Mewdeko.Modules.Searches", "Mewdeko.Modules.Server_Management", "Discord.Interactions")
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

        var sw1 = Stopwatch.StartNew();
        var cs = CSharpScript.Create(modal.Code, sopts, typeof(InteractionEvaluationEnvironment));
        var csc = cs.Compile();
        sw1.Stop();

        if (csc.Any(xd => xd.Severity == DiagnosticSeverity.Error))
        {
            embed = new EmbedBuilder
            {
                Title = "Compilation failed",
                Description =
                    $"Compilation failed after {sw1.ElapsedMilliseconds:#,##0}ms with {csc.Length:#,##0} errors.",
                Color = new Color(0xD091B2)
            };
            foreach (var xd in csc.Take(3))
            {
                var ls = xd.Location.GetLineSpan();
                embed.AddField($"Error at {ls.StartLinePosition.Line:#,##0}, {ls.StartLinePosition.Character:#,##0}", Format.Code(xd.GetMessage()));
            }

            if (csc.Length > 3)
                embed.AddField("Some errors omitted", $"{csc.Length - 3:#,##0} more errors not displayed");
            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        Exception rex;
        ScriptState<object> css = default;
        var sw2 = Stopwatch.StartNew();
        try
        {
            css = await cs.RunAsync(globals).ConfigureAwait(false);
            rex = css.Exception;
        }
        catch (Exception ex)
        {
            rex = ex;
        }

        sw2.Stop();

        if (rex != null)
        {
            embed = new EmbedBuilder
            {
                Title = "Execution failed",
                Description =
                    $"Execution failed after {sw2.ElapsedMilliseconds:#,##0}ms with `{rex.GetType()}: {rex.Message}`.",
                Color = new Color(0xD091B2)
            };
            await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        // execution succeeded
        embed = new EmbedBuilder
        {
            Title = "Evaluation successful", Color = new Color(0xD091B2)
        };

        embed.AddField("Result", css.ReturnValue != null ? css.ReturnValue.ToString() : "No value returned")
            .AddField("Compilation time", $"{sw1.ElapsedMilliseconds:#,##0}ms", true)
            .AddField("Execution time", $"{sw2.ElapsedMilliseconds:#,##0}ms", true);

        if (css.ReturnValue != null)
            embed.AddField("Return type", css.ReturnValue.GetType().ToString(), true);

        await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
    }
}

public sealed class InteractionEvaluationEnvironment
{
    public InteractionEvaluationEnvironment(IInteractionContext ctx) => Ctx = ctx;

    public IInteractionContext Ctx { get; }

    public IDiscordInteraction Interaction => Ctx.Interaction;
    public IMessageChannel Channel => Ctx.Channel;
    public IGuild Guild => Ctx.Guild;
    public IUser User => Ctx.User;
    public IGuildUser Member => (IGuildUser)Ctx.User;
    public DiscordSocketClient Client => Ctx.Client as DiscordSocketClient;
}