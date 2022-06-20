using Discord.Interactions;
using Mewdeko.Common.Attributes.SlashCommands;
using Mewdeko.Modules.Starboard.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Starboard;
[Group("starboard", "Manage the starboard!")]
public class StarboardSlash : MewdekoSlashSubmodule<StarboardService>
{
    private readonly GuildSettingsService _guildSettings;

    public StarboardSlash(GuildSettingsService guildSettings)
    {
        _guildSettings = guildSettings;
    }

    [SlashCommand("starboard", "Set the starboard channel. Put nothing to disable."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetStarboard(ITextChannel? chn = null)
    {
        if (chn is null)
        {
            await Service.SetStarboardChannel(ctx.Guild, 0);
            await ctx.Interaction.SendConfirmAsync("Starboard has been disabled.");
            return;
        }

        await Service.SetStarboardChannel(ctx.Guild, chn.Id);
        await ctx.Interaction.SendConfirmAsync($"Channel set to {chn.Mention}");
    }

    [SlashCommand("repostthreshold", "Set after how many messages mewdeko reposts a starboard message"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetRepostThreshold(int num)
    {
        if (num == 0)
        {
            await ctx.Interaction.SendConfirmAsync("Reposting disabled!");
            await Service.SetRepostThreshold(ctx.Guild, 0);
            return;
        }
        await Service.SetRepostThreshold(ctx.Guild, num);
        await ctx.Interaction.SendConfirmAsync($"Successfully set the Repost Threshold to {num}");
    }

    [SlashCommand("stars", "Sets after how many reactions a message gets sent to the starboard"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetStars(int num)
    {
        var count = Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num);
        var count2 = Service.GetStarCount(ctx.Guild.Id);
        await ctx.Interaction.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!");
    }

    [SlashCommand("star", "Sets or gets the current starboard emote"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetStar(string? emoteText = null)
    {
        await ctx.Interaction.DeferAsync();
        if (emoteText is null)
        {
            var maybeEmote = Service.GetStar(ctx.Guild.Id).ToIEmote();
            if (maybeEmote.Name is null)
            {
                await ctx.Interaction.SendErrorFollowupAsync("You don't have an emote set!");
                return;
            }

            await ctx.Interaction.SendConfirmFollowupAsync(
                $"Your current starboard emote is {maybeEmote} {Format.Code(maybeEmote.ToString())}");
            return;
        }

        var emote = emoteText.ToIEmote();
        IUserMessage msg = null;
        try
        {
            msg = await ctx.Interaction.SendConfirmFollowupAsync("Testing emote...");
            await msg.AddReactionAsync(emote);
        }
        catch
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorFollowupAsync("This emote cannot be used! Please use a different one.");
            return;
        }
        await Service.SetStar(ctx.Guild, emote.ToString());
        await ctx.Interaction.SendConfirmFollowupAsync($"Successfully set the star to {emote}");
    }

    [SlashCommand("channel-toggle", "Adds a channel to the whitelist/blacklist"), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardChToggle(ITextChannel channel)
    {
        if (!await Service.ToggleChannel(ctx.Guild, channel.Id.ToString()))
        {
            await ctx.Interaction.SendConfirmAsync($"{channel.Mention} has been added to the whitelist/blacklist (Depnding on what was set in {_guildSettings.GetPrefix(ctx.Guild)}swm)");
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync($"{channel.Mention} has been removed from the whitelist/blacklist (Depending on what was set in {_guildSettings.GetPrefix(ctx.Guild)}swm)");
        }
    }

    [SlashCommand("whitelist-mode", "Sets wether starboard is in white or blacklist mode"), SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardWlMode(Starboard.WhitelistMode mode)
    {
        if (mode > 0)
        {
            await Service.SetCheckMode(ctx.Guild, true);
            await ctx.Interaction.SendConfirmAsync("Starboard Blacklist has been enabled");
        }
        else
        {
            await Service.SetCheckMode(ctx.Guild, false);
            await ctx.Interaction.SendConfirmAsync("Starboard Whitelist mode has been enabled");
        }
    }

    [SlashCommand("removeonreactionclear", "Sets wether a post is removed when the source reactions are cleared."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnReactionsCleared(bool enabled)
    {
        await Service.SetRemoveOnClear(ctx.Guild, enabled);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the message's reactions are cleared.");
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed upon clearing reactions.");
    }

    [SlashCommand("removeondelete", "Sets wehter a post gets removed when the source gets deleted."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnDelete(bool enabled)
    {
        await Service.SetRemoveOnDelete(ctx.Guild, enabled);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the original message is deleted.");
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed upon original message deletion.");
    }

    [SlashCommand("removeonbelowthreshold", "Sets wether a post is removed when its below the set star count."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardRemoveOnBelowThreshold(bool enabled)
    {
        await Service.SetRemoveOnBelowThreshold(ctx.Guild, enabled);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard posts will now be removed when the messages star count is below the current star count.");
        else
            await ctx.Interaction.SendConfirmAsync("Starboard posts will no longer be removed when the messages star count is below the current star count");
    }

    [SlashCommand("allowbots", "Sets wether starboard ignores bots."), SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task StarboardAllowBots(bool enabled)
    {
        await Service.SetStarboardAllowBots(ctx.Guild, enabled);
        if (enabled)
            await ctx.Interaction.SendConfirmAsync("Starboard will no longer ignore bots.");
        else
            await ctx.Interaction.SendConfirmAsync("Starboard will now ignore bots.");
    }
}