using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Modules.Starboard;

/// <summary>
///     Class for managing starboard commands via slash commands.
/// </summary>
[Group("starboard", "Manage the starboard!")]
public class StarboardSlash(GuildSettingsService guildSettings) : MewdekoSlashSubmodule<StarboardService>
{
    /// <summary>
    ///     Sets the starboard channel. Put nothing to disable.
    /// </summary>
    /// <param name="chn">The channel to set as the starboard channel, or null to disable the starboard feature.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("setstarboard", "Set the starboard channel. Put nothing to disable.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetStarboard(ITextChannel? chn = null)
    {
        if (chn is null)
        {
            await Service.SetStarboardChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Starboard has been disabled.").ConfigureAwait(false);
            return;
        }

        await Service.SetStarboardChannel(ctx.Guild, chn.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Channel set to {chn.Mention}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Set after how many messages Mewdeko reposts a starboard message.
    /// </summary>
    /// <param name="num">The threshold value for reposting a starboard message.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("repostthreshold", "Set after how many messages mewdeko reposts a starboard message")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetRepostThreshold(int num)
    {
        if (num == 0)
        {
            await ctx.Interaction.SendConfirmAsync("Reposting disabled!").ConfigureAwait(false);
            await Service.SetRepostThreshold(ctx.Guild, 0).ConfigureAwait(false);
            return;
        }

        await Service.SetRepostThreshold(ctx.Guild, num).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Successfully set the Repost Threshold to {num}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets after how many reactions a message gets sent to the starboard.
    /// </summary>
    /// <param name="num">The number of reactions required for a message to be sent to the starboard.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("stars", "Sets after how many reactions a message gets sent to the starboard")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetStars(int num)
    {
        var count = await Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num).ConfigureAwait(false);
        var count2 = await Service.GetStarCount(ctx.Guild.Id);
        await ctx.Interaction.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets or gets the current starboard emote.
    /// </summary>
    /// <param name="emoteText">
    ///     The string representation of the emote to set as the starboard emote, or null to retrieve the
    ///     current emote.
    /// </param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("star", "Sets or gets the current starboard emote")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetStar(string? emoteText = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        if (emoteText is null)
        {
            var maybeEmote = (await Service.GetStar(ctx.Guild.Id)).ToIEmote();
            if (maybeEmote.Name is null)
            {
                await ctx.Interaction.SendErrorFollowupAsync("You don't have an emote set!", Config)
                    .ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendConfirmFollowupAsync(
                    $"Your current starboard emote is {maybeEmote} {Format.Code(maybeEmote.ToString())}")
                .ConfigureAwait(false);
            return;
        }

        var emote = emoteText.ToIEmote();
        var msg = await ctx.Interaction.SendConfirmFollowupAsync("Testing emote...").ConfigureAwait(false);
        try
        {
            await msg.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await msg.DeleteAsync().ConfigureAwait(false);
            await ctx.Interaction
                .SendErrorFollowupAsync("This emote cannot be used! Please use a different one.", Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SetStar(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync($"Successfully set the star to {emote}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a channel to the whitelist/blacklist for the starboard feature.
    /// </summary>
    /// <param name="channel">The channel to add to the whitelist/blacklist.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("channel-toggle", "Adds a channel to the whitelist/blacklist")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardChToggle(ITextChannel channel)
    {
        if (!await Service.ToggleChannel(ctx.Guild, channel.Id.ToString()).ConfigureAwait(false))
        {
            await ctx.Interaction
                .SendConfirmAsync(
                    $"{channel.Mention} has been added to the whitelist/blacklist (Depnding on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction
                .SendConfirmAsync(
                    $"{channel.Mention} has been removed from the whitelist/blacklist (Depending on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets whether starboard is in whitelist or blacklist mode.
    /// </summary>
    /// <param name="mode">The mode to set for the starboard feature.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("whitelist-mode", "Sets wether starboard is in white or blacklist mode")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardWlMode(Starboard.WhitelistMode mode)
    {
        if (mode > 0)
        {
            await Service.SetCheckMode(ctx.Guild, true).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Starboard Blacklist has been enabled").ConfigureAwait(false);
        }
        else
        {
            await Service.SetCheckMode(ctx.Guild, false).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Starboard Whitelist mode has been enabled").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets whether a post is removed when the source reactions are cleared.
    /// </summary>
    /// <param name="enabled">True to enable the feature, false to disable it.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("removeonreactionclear", "Sets wether a post is removed when the source reactions are cleared.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnReactionsCleared(bool enabled)
    {
        await Service.SetRemoveOnClear(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction
                .SendConfirmAsync("Starboard posts will now be removed when the message's reactions are cleared.")
                .ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed upon clearing reactions.")
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether a post gets removed when the source gets deleted.
    /// </summary>
    /// <param name="enabled">True to enable the feature, false to disable it.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("removeondelete", "Sets wehter a post gets removed when the source gets deleted.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnDelete(bool enabled)
    {
        await Service.SetRemoveOnDelete(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction
                .SendConfirmAsync("Starboard posts will now be removed when the original message is deleted.")
                .ConfigureAwait(false);
        else
            await ctx.Interaction
                .SendConfirmAsync("Starboard posts will no longer be removed upon original message deletion.")
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether a post is removed when it's below the set star count.
    /// </summary>
    /// <param name="enabled">True to enable the feature, false to disable it.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("removeonbelowthreshold", "Sets wether a post is removed when its below the set star count.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnBelowThreshold(bool enabled)
    {
        await Service.SetRemoveOnBelowThreshold(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction
                .SendConfirmAsync(
                    "Starboard posts will now be removed when the messages star count is below the current star count.")
                .ConfigureAwait(false);
        else
            await ctx.Interaction
                .SendConfirmAsync(
                    "Starboard posts will no longer be removed when the messages star count is below the current star count")
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether starboard ignores bots.
    /// </summary>
    /// <param name="enabled">True to enable the feature, false to disable it.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    [SlashCommand("allowbots", "Sets wether starboard ignores bots.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardAllowBots(bool enabled)
    {
        await Service.SetStarboardAllowBots(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard will no longer ignore bots.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard will now ignore bots.").ConfigureAwait(false);
    }
}