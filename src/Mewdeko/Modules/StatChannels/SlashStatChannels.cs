using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
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
    [SlashCommand("create", "Create a new stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Create(
        [Choice("Total Members", 0)]
        [Choice("Human Members", 1)]
        [Choice("Bot Count", 2)]
        [Choice("Online Members", 3)]
        [Choice("Channel Count", 5)]
        [Choice("Role Count", 6)]
        [Choice("Boost Count", 7)]
        [Choice("Boost Level", 8)]
        [Choice("Emoji Count", 9)]
        int type,
        string? template = null,
        ICategoryChannel? category = null)
    {
        await DeferAsync().ConfigureAwait(false);

        var statType = (StatChannelType)type;
        template ??= statType switch
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
            var (sc, vc) = await Service.CreateStatChannelAsync(ctx.Guild, statType, template, category?.Id);
            await FollowupAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithDescription(Strings.StatChannelCreated(ctx.Guild.Id, vc.Name, statType.ToString())).Build())
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
    /// <param name="template">The display template with %count% placeholder.</param>
    [SlashCommand("add", "Use an existing voice channel as a stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Add(IVoiceChannel channel,
        [Choice("Total Members", 0)]
        [Choice("Human Members", 1)]
        [Choice("Bot Count", 2)]
        [Choice("Online Members", 3)]
        [Choice("Channel Count", 5)]
        [Choice("Role Count", 6)]
        [Choice("Boost Count", 7)]
        [Choice("Boost Level", 8)]
        [Choice("Emoji Count", 9)]
        int type,
        string? template = null)
    {
        var statType = (StatChannelType)type;
        template ??= statType switch
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
            await Service.AddStatChannelAsync(ctx.Guild.Id, channel.Id, statType, template);
            await ConfirmAsync(Strings.StatChannelAdded(ctx.Guild.Id, channel.Name, statType.ToString()))
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
    ///     Adds a member goal stat channel.
    /// </summary>
    /// <param name="channel">The voice channel.</param>
    /// <param name="goal">The target member count.</param>
    /// <param name="template">The display template.</param>
    [SlashCommand("goal", "Add a member goal stat channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task Goal(IVoiceChannel channel, int goal, string? template = null)
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
            var ch = (ctx.Guild as SocketGuild)?.GetVoiceChannel(sc.ChannelId);
            var name = ch?.Name ?? "Deleted Channel";
            eb.AddField($"{name}", $"Type: {type} | Template: `{sc.Template}`");
        }

        await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }
}