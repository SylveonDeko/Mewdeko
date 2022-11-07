using Discord.Interactions;
using Mewdeko.Modules.Starboard.Services;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Starboard;
[Group("starboard", "Manage the starboard!")]
public class StarboardSlash : MewdekoSlashSubmodule<StarboardService>
{
    private readonly GuildSettingsService guildSettings;

    public StarboardSlash(GuildSettingsService guildSettings) => this.guildSettings = guildSettings;

    [SlashCommand("starboard", "Set the starboard channel. Put nothing to disable."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
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

    [SlashCommand("repostthreshold", "Set after how many messages mewdeko reposts a starboard message"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
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

    [SlashCommand("stars", "Sets after how many reactions a message gets sent to the starboard"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetStars(int num)
    {
        var count = await Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num).ConfigureAwait(false);
        var count2 = await Service.GetStarCount(ctx.Guild.Id);
        await ctx.Interaction.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!").ConfigureAwait(false);
    }

    [SlashCommand("star", "Sets or gets the current starboard emote"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetStar(string? emoteText = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        if (emoteText is null)
        {
            var maybeEmote = (await Service.GetStar(ctx.Guild.Id)).ToIEmote();
            if (maybeEmote.Name is null)
            {
                await ctx.Interaction.SendErrorFollowupAsync("You don't have an emote set!").ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendConfirmFollowupAsync(
                $"Your current starboard emote is {maybeEmote} {Format.Code(maybeEmote.ToString())}").ConfigureAwait(false);
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
            await ctx.Interaction.SendErrorFollowupAsync("This emote cannot be used! Please use a different one.").ConfigureAwait(false);
            return;
        }
        await Service.SetStar(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync($"Successfully set the star to {emote}").ConfigureAwait(false);
    }

    [SlashCommand("channel-toggle", "Adds a channel to the whitelist/blacklist"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardChToggle(ITextChannel channel)
    {
        if (!await Service.ToggleChannel(ctx.Guild, channel.Id.ToString()).ConfigureAwait(false))
        {
            await ctx.Interaction.SendConfirmAsync($"{channel.Mention} has been added to the whitelist/blacklist (Depnding on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)").ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync($"{channel.Mention} has been removed from the whitelist/blacklist (Depending on what was set in {await guildSettings.GetPrefix(ctx.Guild)}swm)").ConfigureAwait(false);
        }
    }

    [SlashCommand("whitelist-mode", "Sets wether starboard is in white or blacklist mode"), SlashUserPerm(GuildPermission.ManageChannels)]
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

    [SlashCommand("removeonreactionclear", "Sets wether a post is removed when the source reactions are cleared."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnReactionsCleared(bool enabled)
    {
        await Service.SetRemoveOnClear(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the message's reactions are cleared.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed upon clearing reactions.").ConfigureAwait(false);
    }

    [SlashCommand("removeondelete", "Sets wehter a post gets removed when the source gets deleted."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnDelete(bool enabled)
    {
        await Service.SetRemoveOnDelete(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the original message is deleted.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed upon original message deletion.").ConfigureAwait(false);
    }

    [SlashCommand("removeonbelowthreshold", "Sets wether a post is removed when its below the set star count."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnBelowThreshold(bool enabled)
    {
        await Service.SetRemoveOnBelowThreshold(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the messages star count is below the current star count.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed when the messages star count is below the current star count").ConfigureAwait(false);
    }

    [SlashCommand("allowbots", "Sets wether starboard ignores bots."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardAllowBots(bool enabled)
    {
        await Service.SetStarboardAllowBots(ctx.Guild, enabled).ConfigureAwait(false);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard will no longer ignore bots.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Starboard will now ignore bots.").ConfigureAwait(false);
    }
}