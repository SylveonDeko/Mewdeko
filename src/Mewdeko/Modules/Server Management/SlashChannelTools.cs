using DataModel;
using Discord.Interactions;
using Discord.Net;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Slash commands that lock or hide a channel, optionally for a set time, and check permissions.
/// </summary>
/// <param name="inspector">The permission inspector.</param>
[Group("channeltools", "Timed locks and hides, and permission checks for channels")]
public class SlashChannelTools(PermissionInspectorService inspector) : MewdekoSlashModuleBase<ChannelTimedActionService>
{
    private async Task<(bool Ok, TimeSpan? Time)> ParseTimeAsync(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return (true, null);

        TimeSpan span;
        try
        {
            span = StoopidTime.FromInput(time.Trim()).Time;
        }
        catch (ArgumentException)
        {
            await ErrorAsync(Strings.ChantoolsInvalidTime(ctx.Guild.Id));
            return (false, null);
        }

        if (span >= ChannelTimedActionService.MinDuration && span <= ChannelTimedActionService.MaxDuration)
            return (true, span);

        await ErrorAsync(Strings.ChantoolsInvalidTime(ctx.Guild.Id));
        return (false, null);
    }

    private async Task ApplyAsync(IGuildChannel? channel, string? time, IRole?[] roles, ChannelTimedActionKind kind)
    {
        if ((channel ?? ctx.Channel as IGuildChannel) is not SocketGuildChannel target)
            return;

        var (ok, span) = await ParseTimeAsync(time);
        if (!ok)
            return;

        try
        {
            var updated = await Service.ApplyAsync(target, kind, roles.OfType<IRole>().ToList(), span, ctx.User.Id);
            var guildId = ctx.Guild.Id;
            var mention = $"<#{target.Id}>";
            var length = span is { } s ? JoinRoleTimeoutService.FormatMinutes((int)s.TotalMinutes) : "";
            var text = (kind == ChannelTimedActionKind.Lock, updated, span is not null) switch
            {
                (true, true, _) => Strings.ChantoolsLockUpdated(guildId, mention),
                (false, true, _) => Strings.ChantoolsHideUpdated(guildId, mention),
                (true, false, true) => Strings.ChantoolsLockedFor(guildId, mention, length),
                (false, false, true) => Strings.ChantoolsHiddenFor(guildId, mention, length),
                (true, false, false) => Strings.ChantoolsLocked(guildId, mention),
                _ => Strings.ChantoolsHidden(guildId, mention)
            };
            await ConfirmAsync(text);
        }
        catch (HttpException ex)
        {
            await ErrorAsync(Strings.ChantoolsFailed(ctx.Guild.Id, ex.Reason ?? ex.Message));
        }
    }

    private async Task UndoAsync(IGuildChannel? channel, ChannelTimedActionKind kind)
    {
        var target = channel ?? ctx.Channel as IGuildChannel;
        if (target is null)
            return;

        var guildId = ctx.Guild.Id;
        var mention = $"<#{target.Id}>";
        var lockKind = kind == ChannelTimedActionKind.Lock;
        if (!await Service.UndoAsync(target.Id, kind))
        {
            await ErrorAsync(lockKind
                ? Strings.ChantoolsNotLocked(guildId, mention)
                : Strings.ChantoolsNotHidden(guildId, mention));
            return;
        }

        await ConfirmAsync(lockKind
            ? Strings.ChantoolsUnlocked(guildId, mention)
            : Strings.ChantoolsUnhidden(guildId, mention));
    }

    /// <summary>
    ///     Locks a channel by denying Send Messages, optionally for a set time.
    /// </summary>
    /// <param name="channel">The channel, or the current one.</param>
    /// <param name="time">How long the lock lasts.</param>
    /// <param name="role">A role to lock out instead of @everyone.</param>
    /// <param name="role2">Another role to lock out.</param>
    [SlashCommand("lock", "Locks a channel, optionally for a set time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public Task Lock([Summary("channel", "The channel to lock, this one by default")] IGuildChannel? channel = null,
        [Summary("time", "How long it lasts, like 30m, 2h or 1d")]
        string? time = null,
        [Summary("role", "A role to lock out instead of everyone")]
        IRole? role = null,
        [Summary("role2", "Another role to lock out")]
        IRole? role2 = null) => ApplyAsync(channel, time, [role, role2], ChannelTimedActionKind.Lock);

    /// <summary>
    ///     Undoes a lock and puts the overwrites back.
    /// </summary>
    /// <param name="channel">The channel, or the current one.</param>
    [SlashCommand("unlock", "Undoes a lock and puts the overwrites back")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public Task Unlock([Summary("channel", "The channel to unlock, this one by default")] IGuildChannel? channel = null) =>
        UndoAsync(channel, ChannelTimedActionKind.Lock);

    /// <summary>
    ///     Hides a channel by denying View Channel, optionally for a set time.
    /// </summary>
    /// <param name="channel">The channel, or the current one.</param>
    /// <param name="time">How long it stays hidden.</param>
    /// <param name="role">A role to hide it from instead of @everyone.</param>
    /// <param name="role2">Another role to hide it from.</param>
    [SlashCommand("hide", "Hides a channel, optionally for a set time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public Task Hide([Summary("channel", "The channel to hide, this one by default")] IGuildChannel? channel = null,
        [Summary("time", "How long it stays hidden, like 30m, 2h or 1d")]
        string? time = null,
        [Summary("role", "A role to hide it from instead of everyone")]
        IRole? role = null,
        [Summary("role2", "Another role to hide it from")]
        IRole? role2 = null) => ApplyAsync(channel, time, [role, role2], ChannelTimedActionKind.Hide);

    /// <summary>
    ///     Undoes a hide and puts the overwrites back.
    /// </summary>
    /// <param name="channel">The channel, or the current one.</param>
    [SlashCommand("unhide", "Undoes a hide and puts the overwrites back")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public Task Unhide([Summary("channel", "The channel to unhide, this one by default")] IGuildChannel? channel = null) =>
        UndoAsync(channel, ChannelTimedActionKind.Hide);

    /// <summary>
    ///     Lists the channels that are locked or hidden.
    /// </summary>
    [SlashCommand("list", "Lists the locked and hidden channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task List()
    {
        var actions = await Service.ListAsync(ctx.Guild.Id);
        if (actions.Count == 0)
        {
            await ErrorAsync(Strings.ChantoolsListEmpty(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.ChantoolsListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", actions.Take(40).Select(Service.Line)));
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the permissions a member, a role or the bot has in a channel and the overwrites that apply.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="user">The member to check.</param>
    /// <param name="role">The role to check.</param>
    /// <param name="permission">One permission to trace.</param>
    [SlashCommand("permcheck", "Shows a member's, role's or the bot's permissions in a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    public async Task PermCheck([Summary("channel", "The channel to check")] IGuildChannel channel,
        [Summary("user", "The member to check, or leave empty to check the bot")]
        IGuildUser? user = null,
        [Summary("role", "The role to check")] IRole? role = null,
        [Summary("permission", "One permission to trace")]
        ChannelPermission? permission = null)
    {
        if (channel is not SocketGuildChannel target)
            return;

        var subject = user ?? (role is null ? target.Guild.CurrentUser : null);
        var embed = inspector.Check(target, subject, role, permission);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }
}

/// <summary>
///     Slash commands that link a voice channel to a text channel.
/// </summary>
[Group("voicetext", "Give members a text channel while they are in a voice channel")]
public class SlashVoiceText : MewdekoSlashModuleBase<VoiceTextLinkService>
{
    /// <summary>
    ///     Links a voice channel to a text channel.
    /// </summary>
    /// <param name="voice">The voice channel.</param>
    /// <param name="text">The text channel.</param>
    [SlashCommand("link", "Links a voice channel to a text channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task Link([Summary("voice", "The voice channel")] IVoiceChannel voice,
        [Summary("text", "The text channel members get while in the voice channel")]
        ITextChannel text)
    {
        try
        {
            await Service.LinkAsync(ctx.Guild.Id, voice.Id, text.Id);
            await ConfirmAsync(Strings.VtlLinked(ctx.Guild.Id, voice.Mention, text.Mention));
        }
        catch (HttpException ex)
        {
            await ErrorAsync(Strings.ChantoolsFailed(ctx.Guild.Id, ex.Reason ?? ex.Message));
        }
    }

    /// <summary>
    ///     Removes the link of a voice channel.
    /// </summary>
    /// <param name="voice">The voice channel.</param>
    [SlashCommand("unlink", "Removes the link of a voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task Unlink([Summary("voice", "The voice channel")] IVoiceChannel voice)
    {
        if (!await Service.UnlinkAsync(voice.Id))
        {
            await ErrorAsync(Strings.VtlNotLinked(ctx.Guild.Id, voice.Mention));
            return;
        }

        await ConfirmAsync(Strings.VtlUnlinked(ctx.Guild.Id, voice.Mention));
    }

    /// <summary>
    ///     Lists the linked voice channels.
    /// </summary>
    [SlashCommand("list", "Lists the linked voice channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task List()
    {
        var links = Service.GetLinks(ctx.Guild.Id);
        if (links.Count == 0)
        {
            await ErrorAsync(Strings.VtlListEmpty(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.VtlListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n",
                links.Take(40).Select(x => Strings.VtlListLine(ctx.Guild.Id, $"<#{x.VoiceChannelId}>",
                    $"<#{x.TextChannelId}>"))));
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }
}

/// <summary>
///     Slash commands that delete old messages from a channel on a schedule.
/// </summary>
[Group("autopurge", "Delete old messages from a channel on a schedule")]
public class SlashAutoPurge : MewdekoSlashModuleBase<AutoPurgeService>
{
    private async Task<int?> ParseMinutesAsync(string value)
    {
        try
        {
            return (int)StoopidTime.FromInput(value.Trim()).Time.TotalMinutes;
        }
        catch (ArgumentException)
        {
            await ErrorAsync(Strings.ChantoolsInvalidTime(ctx.Guild.Id));
            return null;
        }
    }

    /// <summary>
    ///     Turns on auto-purge for a channel or changes it.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="age">How old a message must be to be deleted.</param>
    /// <param name="filter">Which messages are deleted.</param>
    /// <param name="keepPinned">Whether pinned messages are kept.</param>
    /// <param name="interval">How often the channel is purged.</param>
    [SlashCommand("set", "Turns on auto-purge for a channel or changes it")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task Set([Summary("channel", "The channel to purge")] ITextChannel channel,
        [Summary("age", "How old a message must be to be deleted, like 12h or 1d")]
        string age,
        [Summary("filter", "Which messages are deleted")]
        AutoPurgeFilter filter = AutoPurgeFilter.All,
        [Summary("keep-pinned", "Keep pinned messages, on by default")]
        bool keepPinned = true,
        [Summary("interval", "How often to purge, like 30m. 10m by default")]
        string? interval = null)
    {
        if (await ParseMinutesAsync(age) is not { } ageMinutes)
            return;

        var intervalMinutes = 10;
        if (!string.IsNullOrWhiteSpace(interval))
        {
            if (await ParseMinutesAsync(interval) is not { } parsed)
                return;
            intervalMinutes = parsed;
        }

        var (error, setting) = await Service.SetAsync(ctx.Guild.Id, channel.Id, ageMinutes, intervalMinutes, filter,
            keepPinned);
        switch (error)
        {
            case AutoPurgeError.InvalidAge:
                await ErrorAsync(Strings.ApInvalidAge(ctx.Guild.Id));
                return;
            case AutoPurgeError.InvalidInterval:
                await ErrorAsync(Strings.ApInvalidInterval(ctx.Guild.Id));
                return;
        }

        await ConfirmAsync(Service.Summary(setting!));
    }

    /// <summary>
    ///     Turns off auto-purge for a channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    [SlashCommand("off", "Turns off auto-purge for a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Off([Summary("channel", "The channel")] ITextChannel channel)
    {
        if (!await Service.RemoveAsync(channel.Id))
        {
            await ErrorAsync(Strings.ApNotSet(ctx.Guild.Id, channel.Mention));
            return;
        }

        await ConfirmAsync(Strings.ApOff(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Lists the channels that have auto-purge.
    /// </summary>
    [SlashCommand("list", "Lists the channels that have auto-purge")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task List()
    {
        var settings = await Service.GetAllAsync(ctx.Guild.Id);
        if (settings.Count == 0)
        {
            await ErrorAsync(Strings.ApListEmpty(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.ApListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", settings.Take(40).Select(Service.Line)));
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Purges a channel now with its saved settings.
    /// </summary>
    /// <param name="channel">The channel.</param>
    [SlashCommand("run", "Purges a channel now with its saved settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task Run([Summary("channel", "The channel")] ITextChannel channel)
    {
        await DeferAsync();
        var deleted = await Service.RunNowAsync(channel.Id);
        if (deleted is null)
        {
            await ErrorAsync(Strings.ApRunFailed(ctx.Guild.Id, channel.Mention));
            return;
        }

        await ConfirmAsync(Strings.ApRunDone(ctx.Guild.Id, deleted.Value, channel.Mention));
    }
}

/// <summary>
///     Slash commands that explain why the bot cannot assign a role.
/// </summary>
/// <param name="inspector">The permission inspector.</param>
[Group("diagnose", "Find out why something is not working")]
public class SlashDiagnose(PermissionInspectorService inspector) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Explains why the bot can or cannot assign a role.
    /// </summary>
    /// <param name="role">The role, or none for an overview.</param>
    [SlashCommand("role", "Explains why the bot can or cannot assign a role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    public async Task Role([Summary("role", "The role to check, or leave empty for an overview")] IRole? role = null)
    {
        var embed = inspector.Diagnose((SocketGuild)ctx.Guild, role);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }
}
