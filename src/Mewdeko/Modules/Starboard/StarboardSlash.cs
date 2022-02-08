using Discord;
using Discord.Interactions;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Modules.Starboard;
[Group("starboard", "Manage the starboard!")]
public class StarboardSlash : MewdekoSlashSubmodule<StarboardService>
{
    [SlashCommand("starboard", "Set the starboard channel. Put nothing to disable."), SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task SetStarboard(ITextChannel chn = null)
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

    [SlashCommand("repostthreshold", "Set after how many messages mewdeko reposts a starboard message"), SlashUserPerm(GuildPermission.ManageChannels)]
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

    [SlashCommand("stars", "Sets after how many reactions a message gets sent to the starboard"), SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task SetStars(int num)
    {
        var count = Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num);
        var count2 = Service.GetStarCount(ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!");
    }

    [SlashCommand("star", "Sets or gets the current starboard emote"), SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task SetStar(string emoteText = null)
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
}