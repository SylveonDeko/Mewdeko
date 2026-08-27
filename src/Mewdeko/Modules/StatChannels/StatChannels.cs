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
        template ??= StatChannelDefinitions.DefaultTemplate(type);

        try
        {
            var (_, vc) = await Service.CreateStatChannelAsync(ctx.Guild, type, template);
            await ConfirmAsync(Strings.StatChannelCreated(ctx.Guild.Id, vc.Name,
                StatChannelDefinitions.Get(type)?.Name ?? type.ToString())).ConfigureAwait(false);
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
        template ??= StatChannelDefinitions.DefaultTemplate(type);

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, type, template);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name,
                StatChannelDefinitions.Get(type)?.Name ?? type.ToString())).ConfigureAwait(false);
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
        template ??= StatChannelDefinitions.DefaultTemplate(StatChannelType.RoleMembers);

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
        template ??= StatChannelDefinitions.DefaultTemplate(StatChannelType.Countdown);

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
        template ??= StatChannelDefinitions.DefaultTemplate(StatChannelType.MemberGoal);

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
    ///     Adds a stat channel bound to a named Twitch chat counter.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="counter">The Twitch counter name.</param>
    /// <param name="template">The display template.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelTwitchCounter(IVoiceChannel channel, string counter,
        [Remainder] string? template = null)
    {
        template ??= StatChannelDefinitions.DefaultTemplate(StatChannelType.TwitchCounter);

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, StatChannelType.TwitchCounter, template,
                options: new StatChannelOptions
                {
                    TargetName = counter, DisplayStyle = StatChannelDisplayStyle.Plain
                });
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, $"Twitch Counter: {counter}"))
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await ErrorAsync(Strings.StatChannelExists(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Chooses how a stat channel pushes updates: rename, recreate, or auto.
    /// </summary>
    /// <param name="channel">The stat channel.</param>
    /// <param name="mechanism">The update mechanism.</param>
    /// <param name="interval">How often the channel refreshes, in minutes.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelMechanism(IVoiceChannel channel, StatChannelUpdateMechanism mechanism,
        int? interval = null)
    {
        var sc = await Service.UpdateStatChannelConfigAsync(ctx.Guild.Id, channel.Id, new StatChannelOptions
        {
            Mechanism = mechanism, UpdateIntervalMinutes = interval
        });

        if (sc == null)
        {
            await ErrorAsync(Strings.StatChannelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.StatChannelMechanismSet(ctx.Guild.Id, channel.Name, mechanism.ToString(),
            sc.UpdateIntervalMinutes)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Chooses how a stat channel's number is rendered.
    /// </summary>
    /// <param name="channel">The stat channel.</param>
    /// <param name="style">The display style.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelStyle(IVoiceChannel channel, StatChannelDisplayStyle style)
    {
        var sc = await Service.UpdateStatChannelConfigAsync(ctx.Guild.Id, channel.Id, new StatChannelOptions
        {
            DisplayStyle = style
        });

        if (sc == null)
        {
            await ErrorAsync(Strings.StatChannelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var example = StatChannelFormatter.Format(1234, style, new StatChannelStyleOptions(), 2000);
        await ConfirmAsync(Strings.StatChannelStyleSet(ctx.Guild.Id, channel.Name, style.ToString(), example))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the guild wide defaults applied to newly created stat channels.
    /// </summary>
    /// <param name="mechanism">The default update mechanism.</param>
    /// <param name="interval">The default refresh interval in minutes.</param>
    /// <param name="style">The default display style.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StatChannelDefaults(StatChannelUpdateMechanism? mechanism = null, int? interval = null,
        StatChannelDisplayStyle? style = null)
    {
        var settings = await Service.UpdateSettingsAsync(ctx.Guild.Id, mechanism, interval, style);

        await ConfirmAsync(Strings.StatChannelDefaultsSet(ctx.Guild.Id,
            ((StatChannelUpdateMechanism)settings.DefaultMechanism).ToString(),
            settings.DefaultIntervalMinutes,
            ((StatChannelDisplayStyle)settings.DefaultDisplayStyle).ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Browses the available stat types with worked examples.
    /// </summary>
    /// <param name="category">An optional category to filter by.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task StatChannelTypes([Remainder] string? category = null)
    {
        var definitions = StatChannelDefinitions.All
            .Where(d => category == null || d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (definitions.Count == 0)
        {
            await ErrorAsync(Strings.StatChannelUnknownType(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StatChannelTypesTitle(ctx.Guild.Id));

        foreach (var group in definitions.GroupBy(d => d.Category))
        {
            var value = string.Join("\n", group.Select(d => $"`{d.Type}` **{d.Name}**: {d.Example}"));
            if (value.Length > 1024) value = value[..1021] + "...";
            eb.AddField(group.Key, value);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
            var definition = StatChannelDefinitions.Get(type);
            var ch = (ctx.Guild as SocketGuild)?.GetVoiceChannel(sc.ChannelId);
            var name = ch?.Name ?? "Deleted Channel";
            var mechanism = (StatChannelUpdateMechanism)sc.UpdateMechanism;
            var style = (StatChannelDisplayStyle)sc.DisplayStyle;

            eb.AddField(name,
                $"Type: {definition?.Name ?? type.ToString()} | Style: {style} | " +
                $"Mode: {mechanism} every {sc.UpdateIntervalMinutes}m\nTemplate: `{sc.Template}`");
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }
}