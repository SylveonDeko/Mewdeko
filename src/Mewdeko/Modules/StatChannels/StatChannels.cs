using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.StatChannels.Common;
using Mewdeko.Modules.StatChannels.Services;

namespace Mewdeko.Modules.StatChannels;

/// <summary>
///     Module for managing stat channels that display live server statistics.
/// </summary>
public class StatChannels : MewdekoModuleBase<StatChannelService>
{
    /// <summary>
    ///     Creates a new voice channel as a stat channel.
    /// </summary>
    /// <param name="type">The stat type to display.</param>
    /// <param name="template">The display template with %count% placeholder.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    [BotPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelCreate(StatChannelType type, [Remainder] string? template = null)
    {
        template ??= type switch
        {
            StatChannelType.TotalMembers => "Members: %count%",
            StatChannelType.HumanMembers => "Humans: %count%",
            StatChannelType.BotCount => "Bots: %count%",
            StatChannelType.OnlineMembers => "Online: %count%",
            StatChannelType.ChannelCount => "Channels: %count%",
            StatChannelType.RoleCount => "Roles: %count%",
            StatChannelType.BoostCount => "Boosts: %count%",
            StatChannelType.BoostLevel => "Boost Level: %count%",
            StatChannelType.EmojiCount => "Emojis: %count%",
            _ => "%count%"
        };

        try
        {
            var (sc, vc) = await Service.CreateStatChannelAsync(ctx.Guild, type, template);
            await ConfirmAsync(Strings.StatChannelCreated(ctx.Guild.Id, vc.Name, type.ToString()))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ErrorAsync(ex.Message).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a voice channel as a stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="type">The stat type to display.</param>
    /// <param name="template">The display template with %count% placeholder.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelAdd(IVoiceChannel channel, StatChannelType type, [Remainder] string? template = null)
    {
        template ??= type switch
        {
            StatChannelType.TotalMembers => "Members: %count%",
            StatChannelType.HumanMembers => "Humans: %count%",
            StatChannelType.BotCount => "Bots: %count%",
            StatChannelType.OnlineMembers => "Online: %count%",
            StatChannelType.ChannelCount => "Channels: %count%",
            StatChannelType.RoleCount => "Roles: %count%",
            StatChannelType.BoostCount => "Boosts: %count%",
            StatChannelType.BoostLevel => "Boost Level: %count%",
            StatChannelType.EmojiCount => "Emojis: %count%",
            _ => "%count%"
        };

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, type, template);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, type.ToString()))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.StatChannelExists(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a role member count stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="role">The role to count.</param>
    /// <param name="template">The display template.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelRole(IVoiceChannel channel, IRole role, [Remainder] string? template = null)
    {
        template ??= $"{role.Name}: %count%";

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, StatChannelType.RoleMembers, template,
                role.Id);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, $"Role: {role.Name}"))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.StatChannelExists(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a countdown stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="date">The target date.</param>
    /// <param name="template">The display template.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelCountdown(IVoiceChannel channel, DateTime date, [Remainder] string? template = null)
    {
        template ??= "Days until event: %days%";

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, StatChannelType.Countdown, template,
                countdownDate: date);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, "Countdown"))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.StatChannelExists(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a member goal stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="goal">The target member count.</param>
    /// <param name="template">The display template.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelGoal(IVoiceChannel channel, int goal, [Remainder] string? template = null)
    {
        template ??= "Members: %count% / %goal%";

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, StatChannelType.MemberGoal, template,
                goalTarget: goal);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, $"Goal: {goal}"))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.StatChannelExists(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelRemove(IVoiceChannel channel)
    {
        var removed = await Service.RemoveStatChannelAsync(ctx.Guild.Id, channel.Id);
        if (removed)
            await ConfirmAsync(Strings.StatChannelRemoved(ctx.Guild.Id, channel.Name)).ConfigureAwait(false);
        else
            await ErrorAsync(Strings.StatChannelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all stat channels for this guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task StatChannelList()
    {
        var channels = await Service.GetStatChannelsAsync(ctx.Guild.Id);
        if (channels.Count == 0)
        {
            await ErrorAsync(Strings.StatChannelNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StatChannelListTitle(ctx.Guild.Id));

        foreach (var sc in channels)
        {
            var type = (StatChannelType)sc.StatType;
            var ch = (ctx.Guild as SocketGuild)?.GetVoiceChannel(sc.ChannelId);
            var name = ch?.Name ?? "Deleted Channel";
            eb.AddField($"{name}", $"Type: {type} | Template: `{sc.Template}`");
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }
}