using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.StatChannels.Common;
using Mewdeko.Modules.StatChannels.Services;

namespace Mewdeko.Modules.StatChannels;

/// <summary>
///     Slash command module for managing stat channels.
/// </summary>
[Group("statchannel", "Manage stat channels that display live server statistics")]
public class SlashStatChannels : MewdekoSlashModuleBase<StatChannelService>
{
    /// <summary>
    ///     Creates a new voice channel as a stat channel.
    /// </summary>
    /// <param name="type">The stat type.</param>
    /// <param name="template">The display template.</param>
    /// <param name="category">The category to create the channel in.</param>
    /// <param name="style">The display style applied to the number.</param>
    /// <param name="mechanism">How updates are pushed to Discord.</param>
    /// <param name="interval">How often the channel refreshes, in minutes.</param>
    [SlashCommand("create", "Create a new stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Create(
        [Autocomplete(typeof(StatChannelTypeAutocompleter))]
        int type,
        string? template = null,
        ICategoryChannel? category = null,
        StatChannelDisplayStyle? style = null,
        StatChannelUpdateMechanism? mechanism = null,
        int? interval = null)
    {
        await DeferAsync().ConfigureAwait(false);

        var statType = (StatChannelType)type;
        var definition = StatChannelDefinitions.Get(statType);
        if (definition == null)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.StatChannelUnknownType(ctx.Guild.Id)).Build()).ConfigureAwait(false);
            return;
        }

        template ??= definition.DefaultTemplate;

        try
        {
            var (_, vc) = await Service.CreateStatChannelAsync(ctx.Guild, statType, template, category?.Id,
                options: BuildOptions(style ?? definition.RecommendedStyle, mechanism, interval));

            await FollowupAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithDescription(Strings.StatChannelCreated(ctx.Guild.Id, vc.Name, definition.Name)).Build())
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(ex.Message).Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds an existing voice channel as a stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to use.</param>
    /// <param name="type">The stat type to display.</param>
    /// <param name="template">The display template.</param>
    /// <param name="style">The display style applied to the number.</param>
    /// <param name="mechanism">How updates are pushed to Discord.</param>
    /// <param name="interval">How often the channel refreshes, in minutes.</param>
    [SlashCommand("add", "Use an existing voice channel as a stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Add(IVoiceChannel channel,
        [Autocomplete(typeof(StatChannelTypeAutocompleter))]
        int type,
        string? template = null,
        StatChannelDisplayStyle? style = null,
        StatChannelUpdateMechanism? mechanism = null,
        int? interval = null)
    {
        var statType = (StatChannelType)type;
        var definition = StatChannelDefinitions.Get(statType);
        if (definition == null)
        {
            await ErrorAsync(Strings.StatChannelUnknownType(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        template ??= definition.DefaultTemplate;

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, statType, template,
                options: BuildOptions(style ?? definition.RecommendedStyle, mechanism, interval));
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, definition.Name))
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
    /// <param name="channel">The voice channel.</param>
    /// <param name="role">The role to count.</param>
    /// <param name="template">The display template.</param>
    [SlashCommand("role", "Add a stat channel for role member count")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Role(IVoiceChannel channel, IRole role, string? template = null)
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
    ///     Adds a member goal stat channel.
    /// </summary>
    /// <param name="channel">The voice channel.</param>
    /// <param name="goal">The target member count.</param>
    /// <param name="template">The display template.</param>
    /// <param name="style">The display style, for example a progress bar.</param>
    [SlashCommand("goal", "Add a member goal stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Goal(IVoiceChannel channel, int goal, string? template = null,
        StatChannelDisplayStyle? style = null)
    {
        template ??= StatChannelDefinitions.DefaultTemplate(StatChannelType.MemberGoal);

        try
        {
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, StatChannelType.MemberGoal, template,
                goalTarget: goal, options: BuildOptions(style, null, null));
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
    /// <param name="channel">The voice channel.</param>
    /// <param name="counter">The Twitch counter name.</param>
    /// <param name="template">The display template.</param>
    [SlashCommand("twitchcounter", "Mirror a Twitch chat counter into a voice channel name")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task TwitchCounter(IVoiceChannel channel, string counter, string? template = null)
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
    ///     Changes how an existing stat channel pushes updates to Discord.
    /// </summary>
    /// <param name="channel">The stat channel.</param>
    /// <param name="mechanism">The update mechanism.</param>
    /// <param name="interval">How often the channel refreshes, in minutes.</param>
    [SlashCommand("mechanism", "Choose how a stat channel updates: rename, recreate, or auto")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Mechanism(IVoiceChannel channel, StatChannelUpdateMechanism mechanism, int? interval = null)
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
    ///     Changes the display style of an existing stat channel.
    /// </summary>
    /// <param name="channel">The stat channel.</param>
    /// <param name="style">The display style.</param>
    [SlashCommand("style", "Choose how a stat channel's number is rendered")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Style(IVoiceChannel channel, StatChannelDisplayStyle style)
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
    [SlashCommand("defaults", "Set the defaults applied to new stat channels")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Defaults(StatChannelUpdateMechanism? mechanism = null, int? interval = null,
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
    [SlashCommand("types", "Browse every available stat type with examples")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Types(string? category = null)
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
            var lines = group.Select(d => $"`{(int)d.Type}` **{d.Name}**: {d.Example}");
            var value = string.Join("\n", lines);
            if (value.Length > 1024) value = value[..1021] + "...";
            eb.AddField(group.Key, value);
        }

        await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a stat channel.
    /// </summary>
    /// <param name="channel">The voice channel to remove.</param>
    [SlashCommand("remove", "Remove a stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Remove(IVoiceChannel channel)
    {
        var removed = await Service.RemoveStatChannelAsync(ctx.Guild.Id, channel.Id);
        if (removed)
            await ConfirmAsync(Strings.StatChannelRemoved(ctx.Guild.Id, channel.Name)).ConfigureAwait(false);
        else
            await ErrorAsync(Strings.StatChannelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all stat channels.
    /// </summary>
    [SlashCommand("list", "List all stat channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task List()
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

        await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    private static StatChannelOptions? BuildOptions(StatChannelDisplayStyle? style,
        StatChannelUpdateMechanism? mechanism, int? interval)
    {
        if (style == null && mechanism == null && interval == null)
            return null;

        return new StatChannelOptions
        {
            DisplayStyle = style, Mechanism = mechanism, UpdateIntervalMinutes = interval
        };
    }
}