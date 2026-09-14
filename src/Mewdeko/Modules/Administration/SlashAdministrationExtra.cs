using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashAdministration
{
    /// <summary>
    ///     The scope a delete-message-on-command setting applies to.
    /// </summary>
    public enum DelmsgoncmdScope
    {
        /// <summary>
        ///     Toggles the setting for the whole server.
        /// </summary>
        Server,

        /// <summary>
        ///     Sets the state for a single channel.
        /// </summary>
        Channel
    }

    /// <summary>
    ///     Options for the member purge commands that filter by join or account age.
    /// </summary>
    public enum UnderOption
    {
        /// <summary>
        ///     Only filter by how long the member has been in the server.
        /// </summary>
        ServerAge,

        /// <summary>
        ///     Also filter by how old the account is, using the second time.
        /// </summary>
        AccountAge,

        /// <summary>
        ///     Show a preview of the members before confirming.
        /// </summary>
        Preview
    }

    /// <summary>
    ///     Slash commands for creating, deleting and editing channels.
    /// </summary>
    [Group("channel", "Create, delete and edit channels")]
    public class AdministrationChannel : MewdekoSlashSubmodule<AdministrationService>
    {
        /// <summary>
        ///     Creates a new text channel with the specified name.
        /// </summary>
        /// <param name="channelName">The name of the text channel to create</param>
        [SlashCommand("create-text", "Creates a new text channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task CreaTxtChanl([Summary("name", "The name of the channel to create")] string channelName)
        {
            var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Createtextchan(ctx.Guild.Id, Format.Bold(txtCh.Name)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes the specified text channel.
        /// </summary>
        /// <param name="toDelete">The text channel to delete</param>
        [SlashCommand("delete-text", "Deletes a text channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task DelTxtChanl([Summary("channel", "The text channel to delete")] ITextChannel toDelete)
        {
            await toDelete.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Deltextchan(ctx.Guild.Id, Format.Bold(toDelete.Name)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a new voice channel with the specified name.
        /// </summary>
        /// <param name="channelName">The name of the voice channel to create</param>
        [SlashCommand("create-voice", "Creates a new voice channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task CreatVoiChanl(
            [Summary("name", "The name of the channel to create")]
            string channelName)
        {
            var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Createvoich(ctx.Guild.Id, Format.Bold(ch.Name))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes the specified voice channel.
        /// </summary>
        /// <param name="voiceChannel">The voice channel to delete</param>
        [SlashCommand("delete-voice", "Deletes a voice channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task DelVoiChanl(
            [Summary("channel", "The voice channel to delete")]
            IVoiceChannel voiceChannel)
        {
            await voiceChannel.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Delvoich(ctx.Guild.Id, Format.Bold(voiceChannel.Name)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Renames the specified channel, or the current channel when none is given.
        /// </summary>
        /// <param name="name">The new name for the channel</param>
        /// <param name="channel">The channel to rename. Defaults to the current channel.</param>
        [SlashCommand("rename", "Renames a channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameChannel([Summary("name", "The new name for the channel")] string name,
            [Summary("channel", "The channel to rename, defaults to the current channel")]
            IGuildChannel? channel = null)
        {
            if (channel is null)
            {
                var current = (ITextChannel)ctx.Channel;
                await current.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.SetChannelName(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await channel.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
            await ConfirmAsync(Strings.ChannelRenamed(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the topic of the current text channel. Leave the topic empty to clear it.
        /// </summary>
        /// <param name="topic">The topic to set for the text channel</param>
        [SlashCommand("topic", "Sets the topic of the current text channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task SetTopic(
            [Summary("topic", "The new topic, leave empty to clear")]
            string? topic = null)
        {
            var channel = (ITextChannel)ctx.Channel;
            topic ??= "";
            await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.SetTopic(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the NSFW setting of the current text channel.
        /// </summary>
        [SlashCommand("nsfw-toggle", "Toggles the nsfw flag of the current text channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task NsfwToggle()
        {
            var channel = (ITextChannel)ctx.Channel;
            var isEnabled = channel.IsNsfw;

            await channel.ModifyAsync(c => c.IsNsfw = !isEnabled).ConfigureAwait(false);

            if (isEnabled)
                await ReplyConfirmAsync(Strings.NsfwSetFalse(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.NsfwSetTrue(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Slash commands for banning, kicking and pruning members in bulk.
    /// </summary>
    /// <param name="interactivity">The interactivity service by Fergun.Interactive</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="banPrune">The service resolving how many days of messages a ban purges.</param>
    /// <param name="guildSettings">Guild settings service for prefix lookups.</param>
    [Group("purge-members", "Ban, kick or prune members in bulk")]
    public class AdministrationPurgeMembers(
        InteractiveService interactivity,
        ILogger<AdministrationPurgeMembers> logger,
        BanPruneService banPrune,
        GuildSettingsService guildSettings) : MewdekoSlashSubmodule<AdministrationService>
    {
        /// <summary>
        ///     Allows you to ban users that have been in the server for a certain amount of time.
        /// </summary>
        /// <param name="time">The maximum time the member has been in the server, for example 1mo</param>
        /// <param name="option">Whether to also check account age, or to preview the users to ban.</param>
        /// <param name="accountAge">The maximum account age, only used with the account age option.</param>
        [SlashCommand("ban-under", "Bans every member that joined less than the given time ago")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanUnder([Summary("time", "The maximum server join age, for example 1mo")] TimeSpan time,
            [Summary("option", "Also check account age, or preview the users first")]
            UnderOption option = UnderOption.ServerAge,
            [Summary("account-age", "The maximum account age, used with the account age option")]
            TimeSpan? accountAge = null)
        {
            try
            {
                await DeferAsync();
                await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
                List<SocketGuildUser> users;
                if (option == UnderOption.AccountAge && accountAge is not null)
                {
                    users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                        c.JoinedAt != null
                        && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.TotalSeconds
                        && DateTimeOffset.Now.Subtract(c.CreatedAt).TotalSeconds <=
                        accountAge.Value.TotalSeconds).ToList();
                }
                else
                {
                    users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                        c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <=
                        time.TotalSeconds).ToList();
                }

                if (users.Count == 0)
                {
                    await ErrorAsync(Strings.BanunderNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                if (option == UnderOption.Preview)
                {
                    var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(users.Count / 20).WithDefaultCanceledPage().WithDefaultEmotes()
                        .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                    await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                            TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                        .ConfigureAwait(false);

                    async Task<PageBuilder> PageFactory(int page)
                    {
                        await Task.CompletedTask.ConfigureAwait(false);
                        return new PageBuilder()
                            .WithTitle(Strings.BanunderPreview(ctx.Guild.Id, users.Count,
                                time.Humanize(maxUnit: TimeUnit.Year)))
                            .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                    }
                }

                var banned = 0;
                var errored = 0;
                if (!await PromptUserConfirmAsync(
                        new EmbedBuilder().WithErrorColor()
                            .WithDescription(Strings.BanunderConfirm(ctx.Guild.Id, users.Count)), ctx.User.Id))
                    return;

                await ConfirmAsync(Strings.BanunderBanning(ctx.Guild.Id, users.Count)).ConfigureAwait(false);
                var underPruneDays =
                    await banPrune.GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.BanUnder, ctx.Channel);
                foreach (var i in users)
                {
                    try
                    {
                        await ctx.Guild.AddBanAsync(i, underPruneDays, options: new RequestOptions
                        {
                            AuditLogReason = Strings.BanunderStarting(ctx.Guild.Id, ctx.User)
                        }).ConfigureAwait(false);
                        banned++;
                    }
                    catch
                    {
                        errored++;
                    }
                }

                await ConfirmAsync(Strings.BanunderBanned(ctx.Guild.Id, banned, errored)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        /// <summary>
        ///     Kicks users who have been in the server for less than a specified time.
        /// </summary>
        /// <param name="time">The maximum time the member has been in the server, for example 1mo</param>
        /// <param name="preview">Whether to preview the users to be kicked before confirming</param>
        [SlashCommand("kick-under", "Kicks every member that joined less than the given time ago")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickUnder([Summary("time", "The maximum server join age, for example 1mo")] TimeSpan time,
            [Summary("preview", "Whether to preview the users before confirming")]
            bool preview = false)
        {
            await DeferAsync();
            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            var guildUsers = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <=
                    time.TotalSeconds)
                .ToArray();
            if (guildUsers.Length == 0)
            {
                await ErrorAsync(Strings.KickunderNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (preview)
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(guildUsers.Length / 20)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();
                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                        TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder()
                        .WithTitle(Strings.KickunderPreview(ctx.Guild.Id, guildUsers.Length,
                            time.Humanize(maxUnit: TimeUnit.Year)))
                        .WithDescription(string.Join("\n", guildUsers.Skip(page * 20).Take(20)));
                }
            }

            var kicked = 0;
            var errored = 0;
            if (!await PromptUserConfirmAsync(
                    new EmbedBuilder().WithErrorColor()
                        .WithDescription(Strings.KickunderConfirm(ctx.Guild.Id, guildUsers.Length)), ctx.User.Id))
                return;

            await ConfirmAsync(Strings.KickunderKicking(ctx.Guild.Id, guildUsers.Length)).ConfigureAwait(false);
            foreach (var i in guildUsers)
            {
                try
                {
                    await i.KickAsync(Strings.KickunderStarting(ctx.Guild.Id, ctx.User)).ConfigureAwait(false);
                    kicked++;
                }
                catch
                {
                    errored++;
                }
            }

            await ConfirmAsync(Strings.KickunderKicked(ctx.Guild.Id, kicked, errored)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Prunes members from the server based on their inactivity.
        /// </summary>
        /// <param name="time">How long a member must have been inactive, for example 30d</param>
        /// <param name="includeMemberRole">Whether to also prune members that have the member role</param>
        [SlashCommand("prune-members", "Prunes members that have been inactive for the given time")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageGuild)]
        public async Task PruneMembers(
            [Summary("time", "How long a member must have been inactive, for example 30d")]
            TimeSpan time,
            [Summary("include-member-role", "Whether to also prune members that have the member role")]
            bool includeMemberRole = false)
        {
            try
            {
                await DeferAsync();
                await ConfirmAsync(Strings.CommandExpectedLatencyServerSize(ctx.Guild.Id));
                if (!includeMemberRole)
                {
                    var toprune = await ctx.Guild.PruneUsersAsync(time.Days, true);
                    if (toprune == 0)
                    {
                        await ErrorAsync(Strings.PruneNoMembersUpsell(ctx.Guild.Id,
                            await guildSettings.GetPrefix(ctx.Guild))).ConfigureAwait(false);
                        return;
                    }

                    var eb = new EmbedBuilder
                    {
                        Description = Strings.PruneConfirm(ctx.Guild.Id, toprune), Color = Mewdeko.OkColor
                    };
                    if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                    {
                        await ConfirmAsync(Strings.PruneCanceledMemberUpsell(ctx.Guild.Id,
                            await guildSettings.GetPrefix(ctx.Guild))).ConfigureAwait(false);
                    }
                    else
                    {
                        await ConfirmAsync(Strings.PruningMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                        await ctx.Guild.PruneUsersAsync(time.Days).ConfigureAwait(false);
                        await ConfirmAsync(Strings.PrunedMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                    }
                }
                else
                {
                    var memberRole = await Service.GetMemberRole(ctx.Guild.Id);
                    var toprune = await ctx.Guild.PruneUsersAsync(time.Days, true,
                        includeRoleIds:
                        [
                            memberRole
                        ]).ConfigureAwait(false);
                    if (toprune == 0)
                    {
                        await ErrorAsync(Strings.PruneNoMembers(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    var eb = new EmbedBuilder
                    {
                        Description = Strings.PruneConfirm(ctx.Guild.Id, toprune), Color = Mewdeko.OkColor
                    };
                    if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                    {
                        await ConfirmAsync(Strings.PruneCanceled(ctx.Guild.Id)).ConfigureAwait(false);
                    }
                    else
                    {
                        await ConfirmAsync(Strings.PruningMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                        await ctx.Guild.PruneUsersAsync(time.Days,
                            includeRoleIds:
                            [
                                memberRole
                            ]);
                        await ConfirmAsync(Strings.PrunedMembers(ctx.Guild.Id, toprune)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogError("Error in prunemembers: \n{0}", exception);
            }
        }
    }

    /// <summary>
    ///     Slash commands for controlling whether command messages are deleted after execution.
    /// </summary>
    [Group("delmsgoncmd", "Control deleting messages that execute commands")]
    public class AdministrationDelMsgOnCmd : MewdekoSlashSubmodule<AdministrationService>
    {
        /// <summary>
        ///     Manages deleting messages on command execution. With no scope, displays the server and channel settings.
        ///     With the server scope, toggles the setting for the whole server. With the channel scope, sets the state for
        ///     a channel.
        /// </summary>
        /// <param name="scope">Whether to toggle the server setting or set a channel state. Omit to list.</param>
        /// <param name="state">The state to set for a channel. Required with the channel scope.</param>
        /// <param name="channel">The channel where the state should be applied. Defaults to the current channel.</param>
        [SlashCommand("set", "Shows, toggles or sets deleting messages that execute commands")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task Delmsgoncmd(
            [Summary("scope", "Server toggles the server setting, channel sets a channel state, omit to list")]
            DelmsgoncmdScope? scope = null,
            [Summary("state", "Enable, disable, or inherit the server setting, used with the channel scope")]
            Administration.State? state = null,
            [Summary("channel", "The channel to apply the state to, defaults to the current channel")]
            ITextChannel? channel = null)
        {
            switch (scope)
            {
                case null:
                {
                    var guild = (SocketGuild)ctx.Guild;
                    var (enabled, channels) = await Service.GetDelMsgOnCmdData(ctx.Guild.Id);

                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(Strings.ServerDelmsgoncmd(ctx.Guild.Id))
                        .WithDescription(enabled ? Strings.Enabled(ctx.Guild.Id) : Strings.Disabled(ctx.Guild.Id));

                    var str = string.Join("\n", channels
                        .Select(x =>
                        {
                            var ch = guild.GetChannel(x.ChannelId)?.ToString()
                                     ?? x.ChannelId.ToString();
                            var prefix = x.State ? Strings.Enabled(ctx.Guild.Id) : Strings.Disabled(ctx.Guild.Id);
                            return $"{prefix} {ch}";
                        }));

                    if (string.IsNullOrWhiteSpace(str))
                        str = "-";

                    embed.AddField(Strings.ChannelDelmsgoncmd(ctx.Guild.Id), str);

                    await RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }
                case DelmsgoncmdScope.Server:
                {
                    if (await Service.ToggleDeleteMessageOnCommand(ctx.Guild.Id))
                    {
                        await ReplyConfirmAsync(Strings.DelmsgOn(ctx.Guild.Id)).ConfigureAwait(false);
                    }
                    else
                    {
                        await ReplyConfirmAsync(Strings.DelmsgOff(ctx.Guild.Id)).ConfigureAwait(false);
                    }

                    return;
                }
            }

            if (state is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var actualChId = channel?.Id ?? ctx.Channel.Id;
            await Service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, state.Value).ConfigureAwait(false);

            switch (state.Value)
            {
                case Administration.State.Disable:
                    await ReplyConfirmAsync(Strings.DelmsgChannelOff(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
                case Administration.State.Enable:
                    await ReplyConfirmAsync(Strings.DelmsgChannelOn(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
                default:
                    await ReplyConfirmAsync(Strings.DelmsgChannelInherit(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>
    ///     Slash commands for voice related administration.
    /// </summary>
    [Group("voice", "Voice related administration")]
    public class AdministrationVoice : MewdekoSlashSubmodule<GameVoiceChannelService>
    {
        /// <summary>
        ///     Deafens specified users in the guild.
        /// </summary>
        /// <param name="users">The users to deafen</param>
        [SlashCommand("deafen", "Deafens the given users")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.DeafenMembers)]
        [RequireBotPermission(GuildPermission.DeafenMembers)]
        public async Task Deafen([Summary("users", "The users to deafen, separated by spaces")] IUser[] users)
        {
            await AdministrationService.DeafenUsers(true, users.OfType<IGuildUser>().ToArray())
                .ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Deafen(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Undeafens specified users in the guild.
        /// </summary>
        /// <param name="users">The users to undeafen</param>
        [SlashCommand("undeafen", "Undeafens the given users")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.DeafenMembers)]
        [RequireBotPermission(GuildPermission.DeafenMembers)]
        public async Task UnDeafen([Summary("users", "The users to undeafen, separated by spaces")] IUser[] users)
        {
            await AdministrationService.DeafenUsers(false, users.OfType<IGuildUser>().ToArray())
                .ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.Undeafen(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the voice channel you are in as the game voice channel for the guild.
        /// </summary>
        [SlashCommand("game-channel", "Toggles your current voice channel as the game voice channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.MoveMembers)]
        public async Task GameVoiceChannel()
        {
            var vch = ((IGuildUser)ctx.User).VoiceChannel;

            if (vch == null)
            {
                await ReplyErrorAsync(Strings.NotInVoice(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var id = await Service.ToggleGameVoiceChannel(ctx.Guild.Id, vch.Id);

            if (id == null)
            {
                await ReplyConfirmAsync(Strings.GvcDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.GvcEnabled(ctx.Guild.Id, Format.Bold(vch.Name))).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Slash commands for overriding the discord permissions a command requires.
    /// </summary>
    /// <param name="interactivity">The interactivity service by Fergun.Interactive</param>
    [Group("perm-override", "Override the discord permissions a command requires")]
    public class AdministrationPermOverride(InteractiveService interactivity)
        : MewdekoSlashSubmodule<DiscordPermOverrideService>
    {
        /// <summary>
        ///     Overrides the required permissions for a specific command in the current guild. Leave the permissions empty
        ///     to remove the override.
        /// </summary>
        /// <param name="command">The command for which the permissions will be overridden</param>
        /// <param name="permissions">The permissions required to execute the command, separated by spaces</param>
        [SlashCommand("set", "Sets the discord permissions a command requires, or removes the override")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverride(
            [Summary("command", "The command to override")] [Autocomplete(typeof(GenericCommandAutocompleter))]
            string command,
            [Summary("permissions", "Space separated permission names, leave empty to remove the override")]
            string? permissions = null)
        {
            var parts = (permissions ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                await Service.RemoveOverride(ctx.Guild.Id, command).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.PermOverrideReset(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var perms = new List<GuildPermission>();
            foreach (var part in parts)
            {
                if (!Enum.TryParse<GuildPermission>(part, true, out var perm))
                {
                    await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                perms.Add(perm);
            }

            var aggregatePerms = perms.Aggregate((acc, seed) => seed | acc);
            await Service.AddOverride(ctx.Guild.Id, command, aggregatePerms).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.PermOverride(ctx.Guild.Id,
                Format.Bold(aggregatePerms.ToString()),
                Format.Code(command))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets all command permission overrides in the current guild.
        /// </summary>
        [SlashCommand("reset", "Removes every discord permission override in this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideReset()
        {
            var result = await PromptUserConfirmAsync(new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.PermOverrideAllConfirm(ctx.Guild.Id)), ctx.User.Id).ConfigureAwait(false);

            if (!result)
                return;
            await Service.ClearAllOverrides(ctx.Guild.Id).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.PermOverrideAll(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all command permission overrides in the current guild.
        /// </summary>
        [SlashCommand("list", "Lists every discord permission override in this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideList()
        {
            var overrides = (await Service.GetAllOverrides(ctx.Guild.Id)).ToList();
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(overrides.Count / 9)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var thisPageOverrides = overrides
                    .Skip(9 * page)
                    .Take(9)
                    .ToList();
                if (thisPageOverrides.Count == 0)
                {
                    return new PageBuilder().WithDescription(Strings.PermOverridePageNone(ctx.Guild.Id))
                        .WithColor(Mewdeko.ErrorColor);
                }

                return new PageBuilder()
                    .WithDescription(string.Join("\n",
                        thisPageOverrides.Select(ov => $"{ov.Command} => {(GuildPermission)ov.Perm}")))
                    .WithColor(Mewdeko.OkColor);
            }
        }
    }
}