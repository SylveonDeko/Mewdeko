using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.strings;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Partial class for server administration slash commands.
/// </summary>
/// <param name="interactivity">The interactivity service by Fergun.Interactive</param>
/// <param name="logger">The logger instance for structured logging.</param>
/// <param name="banPrune">The service resolving how many days of messages a ban purges.</param>
/// <param name="timezoneService">The guild timezone service.</param>
/// <param name="localization">The localization service.</param>
/// <param name="stringsProvider">The strings provider used to list available locales.</param>
[Group("administration", "Server administration stuffs")]
public partial class SlashAdministration(
    InteractiveService interactivity,
    ILogger<SlashAdministration> logger,
    BanPruneService banPrune,
    GuildTimezoneService timezoneService,
    ILocalization localization,
    IBotStringsProvider stringsProvider)
    : MewdekoSlashModuleBase<AdministrationService>
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    /// <summary>
    ///     Gets a cached compiled regex pattern or creates and caches a new one.
    /// </summary>
    /// <param name="pattern">The regex pattern to compile</param>
    /// <returns>A compiled regex with optimized options</returns>
    private static Regex GetCachedRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, static p =>
        {
            try
            {
                return new Regex(p, RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                return new Regex("(?!.*)", RegexOptions.Compiled);
            }
        });
    }

    /// <summary>
    ///     Bans multiple users by their avatar id, aka their avatar hash. Useful for userbots that are stupid.
    /// </summary>
    /// <param name="avatarHash">The avatar hash to search for</param>
    [SlashCommand("ban-by-hash", "Bans every member whose avatar hash matches the given hash")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task BanByHash([Summary("avatar-hash", "The avatar hash to search for")] string avatarHash)
    {
        await DeferAsync();
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users?.Where(x => x.AvatarId == avatarHash).ToList();

        if (usersToBan is null || usersToBan.Count == 0)
        {
            await ErrorAsync(Strings.BanByHashNone(ctx.Guild.Id, avatarHash));
            return;
        }

        if (await PromptUserConfirmAsync(
                Strings.BanByHashConfirm(ctx.Guild.Id, usersToBan.Count, avatarHash), ctx.User.Id))
        {
            await ConfirmAsync(Strings.BanByHashStart(ctx.Guild.Id, usersToBan.Count, avatarHash));
            var failedUsers = 0;
            var bannedUsers = 0;
            var hashPruneDays =
                await banPrune.GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.BanByHash, ctx.Channel);
            foreach (var i in usersToBan)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, hashPruneDays,
                        $"{ctx.User.Id} banning by hash {avatarHash}");
                    bannedUsers++;
                }
                catch
                {
                    failedUsers++;
                }
            }

            if (failedUsers == 0)
                await ConfirmAsync(Strings.BanByHashSuccess(ctx.Guild.Id, bannedUsers, avatarHash));
            else if (failedUsers == usersToBan.Count)
                await ErrorAsync(Strings.BanByHashFailAll(ctx.Guild.Id, usersToBan.Count, avatarHash));
            else
                await ConfirmAsync(Strings.BanByHashFailSome(ctx.Guild.Id, bannedUsers, failedUsers,
                    avatarHash));
        }
    }

    /// <summary>
    ///     Allows you to ban users with a specific role.
    /// </summary>
    /// <param name="role">The role to ban users in</param>
    /// <param name="reason">The reason for the ban, optional</param>
    [SlashCommand("ban-in-role", "Bans every member that has the given role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task BanInRole([Summary("role", "The role to ban users in")] IRole role,
        [Summary("reason", "The reason for the ban")]
        string? reason = null)
    {
        await DeferAsync();
        var users = await ctx.Guild.GetUsersAsync();
        var usersToBan = users.Where(x => x.RoleIds.Contains(role.Id)).ToList();
        if (usersToBan.Count == 0)
        {
            await ErrorAsync(Strings.BanInRoleNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.BanInRoleConfirm(ctx.Guild.Id, usersToBan.Count, role.Mention),
                ctx.User.Id))
        {
            await ErrorAsync(Strings.BanInRoleCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var failedUsers = 0;
        var rolePruneDays = await banPrune.GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.BanInRole, ctx.Channel);
        foreach (var i in usersToBan)
        {
            try
            {
                await ctx.Guild
                    .AddBanAsync(i, rolePruneDays,
                        reason ?? Strings.BanInRoleDefaultReason(ctx.Guild.Id, ctx.User, ctx.User.Id))
                    .ConfigureAwait(false);
            }
            catch
            {
                failedUsers++;
            }
        }

        if (failedUsers == 0)
            await ConfirmAsync(Strings.BanInRoleSuccess(ctx.Guild.Id, usersToBan.Count, role.Mention))
                .ConfigureAwait(false);
        else if (failedUsers == usersToBan.Count)
            await ErrorAsync(Strings.BanInRoleAllFailed(ctx.Guild.Id, users.Count, role.Mention))
                .ConfigureAwait(false);
        else
            await ConfirmAsync(Strings.BanInRolePartialSuccess(ctx.Guild.Id, usersToBan.Count - failedUsers,
                role.Mention,
                failedUsers)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Allows you to ban users with a specific name. This command will show a preview of the users that will be banned.
    ///     Takes a regex pattern as well.
    /// </summary>
    /// <param name="name">The name or regex pattern you want to use.</param>
    /// <param name="deleteDays">How many days of messages to delete for each banned user.</param>
    [SlashCommand("name-ban", "Bans every member whose username matches a name or regex pattern")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task NameBan([Summary("name", "The name or regex pattern to match against usernames")] string name,
        [Summary("delete-days", "How many days of messages to delete, 0 to 7")] [MinValue(0)] [MaxValue(7)]
        int deleteDays = 0)
    {
        await DeferAsync();
        var regex = GetCachedRegex(name);
        var users = (await ctx.Guild.GetUsersAsync()).Where(x => regex.IsMatch(x.Username.ToLower())).ToList();
        if (users.Count == 0)
        {
            await ErrorAsync(Strings.NamebanNoUsersFound(ctx.Guild.Id));
            return;
        }

        var components = new ComponentBuilder()
            .WithButton(Strings.Preview(ctx.Guild.Id), "previewbans")
            .WithButton(Strings.Execute(ctx.Guild.Id), "executeorder66", ButtonStyle.Success)
            .WithButton(Strings.Cancel(ctx.Guild.Id), "cancel", ButtonStyle.Danger);
        var eb = new EmbedBuilder()
            .WithDescription(Strings.PreviewOrExecute(ctx.Guild.Id))
            .WithOkColor();
        var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build(), components: components.Build());
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (input)
        {
            case "cancel":
                await ErrorAsync(Strings.NamebanCancelled(ctx.Guild.Id));
                break;
            case "previewbans":
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(users.Count / 20)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                        TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                    .ConfigureAwait(false);

                break;

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder()
                        .WithTitle(Strings.NamebanPreviewCount(ctx.Guild.Id, users.Count, name.ToLower()))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            case "executeorder66":
                if (await PromptUserConfirmAsync(Strings.NamebanConfirm(ctx.Guild.Id, users.Count), ctx.User.Id))
                {
                    var failedUsers = 0;
                    await ConfirmAsync(Strings.NamebanProcessing(ctx.Guild.Id, users.Count));
                    foreach (var i in users)
                    {
                        try
                        {
                            await ctx.Guild.AddBanAsync(i, deleteDays, options: new RequestOptions
                            {
                                AuditLogReason = Strings.MassBanRequestedBy(ctx.Guild.Id, ctx.User)
                            });
                        }
                        catch
                        {
                            failedUsers++;
                        }
                    }

                    await ConfirmAsync(Strings.NamebanSuccess(ctx.Guild.Id, users.Count - failedUsers,
                        failedUsers));
                }

                break;
            default:
                await ErrorAsync(Strings.NamebanCancelled(ctx.Guild.Id));
                break;
        }
    }

    /// <summary>
    ///     Sets the member role for the server. Currently unused.
    /// </summary>
    /// <param name="role">The role that members will have. Leave empty to show the current role.</param>
    [SlashCommand("member-role", "Sets or shows the member role for the server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task MemberRole([Summary("role", "The role that members will have")] IRole? role = null)
    {
        var rol = await Service.GetMemberRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.MemberRoleSet(ctx.Guild.Id, role.Id)).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorAsync(Strings.MemberRoleAlreadySet(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorAsync(Strings.MemberRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmAsync(Strings.MemberRoleCurrent(ctx.Guild.Id, r.Id)).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.MemberRoleUpdated(ctx.Guild.Id, oldrole.Id, role.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets or updates the role assigned to staff members.
    /// </summary>
    /// <param name="role">The role to be assigned to staff members. Leave empty to show the current role.</param>
    [SlashCommand("staff-role", "Sets or shows the staff role for the server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task StaffRole([Summary("role", "The role to be assigned to staff members")] IRole? role = null)
    {
        var rol = await Service.GetStaffRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.StaffRoleSet(ctx.Guild.Id, role.Id)).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorAsync(Strings.StaffRoleAlreadySet(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorAsync(Strings.StaffRoleMissing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmAsync(Strings.StaffRoleCurrent(ctx.Guild.Id, r.Id)).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.StaffRoleUpdated(ctx.Guild.Id, oldrole.Id, role.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Disables the role assigned to staff members.
    /// </summary>
    [SlashCommand("staff-role-disable", "Disables the staff role for the server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task StaffRoleDisable()
    {
        var r = await Service.GetStaffRole(ctx.Guild.Id);
        if (r == 0)
        {
            await ErrorAsync(Strings.StaffRoleMissing(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await Service.StaffRoleSet(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmAsync(Strings.StaffRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lets you set the nickname for a mentioned user. If no user is provided it defaults to setting a nickname for
    ///     the bot.
    /// </summary>
    /// <param name="nickname">The new nickname. Provide none to reset.</param>
    /// <param name="user">The target user. Leave empty to change the bot's nickname.</param>
    [SlashCommand("set-nick", "Sets the nickname of a user, or of the bot when no user is given")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageNicknames)]
    [RequireBotPermission(GuildPermission.ManageNicknames)]
    public async Task SetNick([Summary("nickname", "The new nickname, leave empty to reset")] string? nickname = null,
        [Summary("user", "The user to rename, leave empty for the bot")]
        IGuildUser? user = null)
    {
        if (user is not null)
        {
            var sg = (SocketGuild)ctx.Guild;
            if (sg.OwnerId == user.Id ||
                user.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
            {
                await ReplyErrorAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(u => u.Nickname = nickname).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.UserNick(ctx.Guild.Id, Format.Bold(user.ToString()),
                    string.IsNullOrWhiteSpace(nickname) ? "-" : Format.Bold(nickname)))
                .ConfigureAwait(false);
            return;
        }

        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        await curUser.ModifyAsync(u => u.Nickname = nickname).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.BotNick(ctx.Guild.Id,
                string.IsNullOrWhiteSpace(nickname) ? "-" : Format.Bold(nickname)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the bot's guild-specific bio.
    /// </summary>
    /// <param name="bio">Bio text</param>
    [SlashCommand("guild-bio", "Sets the bot's bio for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildBio([Summary("bio", "The bio text")] string bio)
    {
        await DeferAsync();
        try
        {
            await Service.SetGuildProfile(ctx.Guild.Id, null, null, bio);
            await ConfirmAsync(Strings.SetGuildBioSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild bio for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild bio for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the bot's guild-specific avatar.
    /// </summary>
    /// <param name="image">An attached image to use as the avatar.</param>
    /// <param name="url">URL to the avatar image, used when no attachment is provided.</param>
    [SlashCommand("guild-avatar", "Sets the bot's avatar for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildAvatar([Summary("image", "The image to use as the avatar")] IAttachment? image = null,
        [Summary("url", "A url to the image, used when no attachment is provided")]
        string? url = null)
    {
        await DeferAsync();
        try
        {
            var imageUrl = image?.Url ?? url;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                await ErrorAsync(Strings.SetGuildProfileNoImage(ctx.Guild.Id));
                return;
            }

            await Service.SetGuildProfile(ctx.Guild.Id, imageUrl, null, null);
            await ConfirmAsync(Strings.SetGuildAvatarSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild avatar for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild avatar for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the bot's guild-specific banner.
    /// </summary>
    /// <param name="image">An attached image to use as the banner.</param>
    /// <param name="url">URL to the banner image, used when no attachment is provided.</param>
    [SlashCommand("guild-banner", "Sets the bot's banner for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetGuildBanner([Summary("image", "The image to use as the banner")] IAttachment? image = null,
        [Summary("url", "A url to the image, used when no attachment is provided")]
        string? url = null)
    {
        await DeferAsync();
        try
        {
            var imageUrl = image?.Url ?? url;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                await ErrorAsync(Strings.SetGuildProfileNoImage(ctx.Guild.Id));
                return;
            }

            await Service.SetGuildProfile(ctx.Guild.Id, null, imageUrl, null);
            await ConfirmAsync(Strings.SetGuildBannerSuccess(ctx.Guild.Id));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting guild banner for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileHttpError(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild banner for guild {GuildId}", ctx.Guild.Id);
            await ErrorAsync(Strings.SetGuildProfileError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Allows you to opt the entire guild out of stats tracking.
    /// </summary>
    [SlashCommand("stats-opt-out", "Toggles whether this server is opted out of command stats tracking")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task GuildStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.Guild);
        if (!optout)
            await ConfirmAsync(Strings.CommandStatsEnabled(ctx.Guild.Id));
        else
            await ConfirmAsync(Strings.CommandStatsDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Allows you to delete all stats data for the guild.
    /// </summary>
    [SlashCommand("delete-stats-data", "Deletes all command stats data for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [InteractionRatelimit(3600)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task DeleteGuildStatsData()
    {
        if (await PromptUserConfirmAsync(Strings.CommandStatsDeleteConfirm(ctx.Guild.Id), ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.Guild))
                await ErrorAsync(Strings.CommandStatsDeleteSuccess(ctx.Guild.Id));
            else
                await ErrorAsync(Strings.CommandStatsDeleteFail(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     List all available time zones.
    /// </summary>
    [SlashCommand("timezones", "Lists all available time zones")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Timezones()
    {
        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(x => x.BaseUtcOffset)
            .ToArray();
        const int timezonesPerPage = 20;

        var curTime = DateTimeOffset.UtcNow;

        var i = 0;
        var timezoneStrings = timezones
            .Select(x => (x, ++i % 2 == 0))
            .Select(data =>
            {
                var (tzInfo, flip) = data;
                var nameStr = $"{tzInfo.Id,-30}";
                var offset = curTime.ToOffset(tzInfo.GetUtcOffset(curTime)).ToString("zzz");
                if (flip)
                    return $"{offset} {Format.Code(nameStr)}";
                return $"{Format.Code(offset)} {nameStr}";
            });

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(timezones.Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithColor(Mewdeko.OkColor)
                .WithTitle(Strings.TimezonesAvailable(ctx.Guild.Id))
                .WithDescription(string.Join("\n",
                    timezoneStrings.Skip(page * timezonesPerPage)
                        .Take(timezonesPerPage)));
        }
    }

    /// <summary>
    ///     Shows the time zone of the guild, or sets it when an id is provided. Setting requires Administrator.
    /// </summary>
    /// <param name="id">The timezone ID to set. Leave empty to show the current time zone.</param>
    [SlashCommand("timezone", "Shows the server time zone, or sets it when an id is given")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Timezone(
        [Summary("id", "The timezone id to set")] [Autocomplete(typeof(TimeZoneAutocompleter))]
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            await ReplyConfirmAsync(Strings.TimezoneGuild(ctx.Guild.Id, timezoneService.GetTimeZoneOrUtc(ctx.Guild.Id)))
                .ConfigureAwait(false);
            return;
        }

        if (!((IGuildUser)ctx.User).GuildPermissions.Has(GuildPermission.Administrator))
        {
            await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        TimeZoneInfo? tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            tz = null;
        }

        if (tz == null)
        {
            await ReplyErrorAsync(Strings.TimezoneNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await timezoneService.SetTimeZone(ctx.Guild.Id, tz);

        await ConfirmAsync(tz.ToString()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the language for the current guild.
    /// </summary>
    /// <param name="name">The name of the language or "default" to reset to the default language.</param>
    [SlashCommand("language-set", "Sets the bot language for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task LanguageSet(
        [Summary("language", "The locale to use, or default to reset")] [Autocomplete(typeof(CultureAutocompleter))]
        string name)
    {
        try
        {
            CultureInfo? ci;
            if (string.Equals(name.Trim(), "default", StringComparison.InvariantCultureIgnoreCase))
            {
                localization.RemoveGuildCulture(ctx.Guild);
                ci = localization.DefaultCultureInfo;
            }
            else
            {
                if (!localization.TryResolveCulture(name, out ci))
                {
                    await ReplyErrorAsync(Strings.LangSetFail(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                localization.SetGuildCulture(ctx.Guild, ci);
            }

            await ReplyConfirmAsync(Strings.LangSet(ctx.Guild.Id, Format.Bold(ci.ToString()),
                    Format.Bold(ci.NativeName)))
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.LangSetFail(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists all supported languages along with their codes.
    /// </summary>
    [SlashCommand("languages-list", "Lists all languages the bot supports")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task LanguagesList()
    {
        var locales = stringsProvider.GetAvailableLocales()
            .OrderBy(x => x)
            .Select(x =>
            {
                string display;
                try
                {
                    display = new CultureInfo(x).NativeName;
                }
                catch (CultureNotFoundException)
                {
                    display = x;
                }

                return $"{Format.Code(x),-10} => {display}";
            });

        await RespondAsync(embed: new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.LangList(ctx.Guild.Id))
            .WithDescription(string.Join("\n", locales)).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Edits a message sent by the bot in the specified text channel. Opens a modal for the new content.
    /// </summary>
    /// <param name="channel">The text channel where the message is located</param>
    /// <param name="messageId">The ID of the message to edit</param>
    [SlashCommand("edit-message", "Edits a message sent by the bot")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Edit([Summary("channel", "The channel the message is in")] ITextChannel channel,
        [Summary("message-id", "The id of the message to edit")]
        ulong messageId)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ViewChannel))
        {
            await ReplyErrorAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<EditMessageModal>($"administration_edit_message:{channel.Id}:{messageId}");
    }

    /// <summary>
    ///     Handles the edit message modal submission and applies the new content to the target message.
    /// </summary>
    /// <param name="channelId">The id of the channel the message is in.</param>
    /// <param name="messageId">The id of the message to edit.</param>
    /// <param name="modal">The submitted modal.</param>
    [ModalInteraction("administration_edit_message:*:*", true)]
    public async Task EditMessageSubmitted(string channelId, string messageId, EditMessageModal modal)
    {
        if (!ulong.TryParse(channelId, out var chanId) || !ulong.TryParse(messageId, out var msgId))
        {
            await EphemeralReplyErrorAsync(Strings.MsgNotFound(ctx.Guild.Id));
            return;
        }

        var channel = await ctx.Guild.GetTextChannelAsync(chanId);
        if (channel is null)
        {
            await EphemeralReplyErrorAsync(Strings.MsgNotFound(ctx.Guild.Id));
            return;
        }

        await DeferAsync(true);
        var msg = await channel.GetMessageAsync(msgId).ConfigureAwait(false);

        if (msg is not IUserMessage umsg || msg.Author.Id != ctx.Client.CurrentUser.Id)
        {
            await EphemeralReplyErrorAsync(Strings.MsgEditNotMine(ctx.Guild.Id));
            return;
        }

        var rep = new ReplacementBuilder()
            .WithDefault(ctx)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(modal.Content), ctx.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            await umsg.ModifyAsync(x =>
            {
                x.Embeds = embed;
                x.Content = plainText?.SanitizeMentions();
                x.Components = components.Build();
            }).ConfigureAwait(false);
        }
        else
        {
            await umsg.ModifyAsync(x =>
            {
                x.Content = modal.Content.SanitizeMentions();
                x.Embed = null;
                x.Components = null;
            }).ConfigureAwait(false);
        }

        await EphemeralReplyConfirmAsync(Strings.MsgEditSuccess(ctx.Guild.Id));
    }

    /// <summary>
    ///     Deletes a message by its ID in the given text channel, optionally after a delay.
    /// </summary>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="channel">The text channel where the message is located. Defaults to the current channel.</param>
    /// <param name="time">Optional time duration after which the message should be deleted</param>
    [SlashCommand("delete-message", "Deletes a message by id, optionally after a delay")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Delete([Summary("message-id", "The id of the message to delete")] ulong messageId,
        [Summary("channel", "The channel the message is in, defaults to the current channel")]
        ITextChannel? channel = null,
        [Summary("time", "How long to wait before deleting, for example 1h30m")]
        TimeSpan? time = null)
    {
        channel ??= (ITextChannel)ctx.Channel;
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorAsync(Strings.InsufPermsI(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var msg = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        if (msg == null)
        {
            await ReplyErrorAsync(Strings.MsgNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (time == null)
        {
            await msg.DeleteAsync().ConfigureAwait(false);
        }
        else if (time.Value <= TimeSpan.FromDays(7))
        {
            var delay = time.Value;
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay).ConfigureAwait(false);
                await msg.DeleteAsync().ConfigureAwait(false);
            });
        }
        else
        {
            await ReplyErrorAsync(Strings.TimeTooLong(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.MessageDeleted(ctx.Guild.Id)).ConfigureAwait(false);
    }
}