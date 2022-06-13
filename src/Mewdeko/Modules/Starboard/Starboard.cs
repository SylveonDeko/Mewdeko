using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Starboard.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Starboard;

public class Starboard : MewdekoSubmodule<StarboardService>
{
    public enum WhitelistMode
    {
        Whitelist = 0,
        Wl = 0,
        White = 0,
        Blacklist = 1,
        Bl = 1,
        Black = 1
    }
    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStarboard(ITextChannel? chn = null)
    {
        if (chn is null)
        {
            await Service.SetStarboardChannel(ctx.Guild, 0);
            await ctx.Channel.SendMessageAsync("Starboard has been disabled.");
            return;
        }

        await Service.SetStarboardChannel(ctx.Guild, chn.Id);
        await ctx.Channel.SendConfirmAsync($"Channel set to {chn.Mention}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task SetRepostThreshold(int num)
    {
        if (num == 0)
        {
            await ctx.Channel.SendErrorAsync("Reposting has been disabled!");
            await Service.SetRepostThreshold(ctx.Guild, 0);
            return;
        }
        await Service.SetRepostThreshold(ctx.Guild, num);
        await ctx.Channel.SendConfirmAsync($"Successfully set the Repost Threshold to {num}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStars(int num)
    {
        var count = Service.GetStarCount(ctx.Guild.Id);
        await Service.SetStarCount(ctx.Guild, num);
        var count2 = Service.GetStarCount(ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync($"Your star count was successfully changed from {count} to {count2}!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStar(IEmote? emote = null)
    {
        if (emote is null)
        {
            var maybeEmote = Service.GetStar(ctx.Guild.Id).ToIEmote();
            if (maybeEmote.Name is null)
            {
                await ctx.Channel.SendErrorAsync("You don't have an emote set!");
                return;
            }

            await ctx.Channel.SendConfirmAsync(
                $"Your current starboard emote is {maybeEmote} {Format.Code(maybeEmote.ToString())}");
            return;
        }

        try
        {
            await ctx.Message.AddReactionAsync(emote);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync("I'm unable to use that emote! Please use a different one.");
            return;
        }
        await Service.SetStar(ctx.Guild, emote.ToString());
        await ctx.Channel.SendConfirmAsync($"Successfully set the star to {emote}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardChToggle([Remainder] ITextChannel channel)
    {
        if (!await Service.ToggleChannel(ctx.Guild, channel.Id.ToString()))
        {
            await ctx.Channel.SendConfirmAsync($"{channel.Mention} has been added to the whitelist/blacklist (Depnding on what was set in {Prefix}swm)");
        }
        else
        {
            await ctx.Channel.SendConfirmAsync($"{channel.Mention} has been removed from the whitelist/blacklist (Depending on what was set in {Prefix}swm)");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardWlMode(WhitelistMode mode)
    {
        if (mode > 0)
        {
            await Service.SetCheckMode(ctx.Guild, true);
            await ctx.Channel.SendConfirmAsync("Starboard Blacklist has been enabled");
        }
        else
        {
            await Service.SetCheckMode(ctx.Guild, false);
            await ctx.Channel.SendConfirmAsync("Starboard Whitelist mode has been enabled");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnReactionsCleared(bool enabled)
    {
        await Service.SetRemoveOnClear(ctx.Guild, enabled);
        if (enabled)
            await ctx.Channel.SendConfirmAsync("Starboard posts will now be removed when the message's reactions are cleared.");
        else
            await ctx.Channel.SendConfirmAsync("Starboard posts will no longer be removed upon clearing reactions.");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnDelete(bool enabled)
    {
        await Service.SetRemoveOnDelete(ctx.Guild, enabled);
        if (enabled)
            await ctx.Channel.SendConfirmAsync("Starboard posts will now be removed when the original message is deleted.");
        else
            await ctx.Channel.SendConfirmAsync("Starboard posts will no longer be removed upon original message deletion.");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnBelowThreshold(bool enabled)
    {
        await Service.SetRemoveOnBelowThreshold(ctx.Guild, enabled);
        if (enabled)
            await ctx.Channel.SendConfirmAsync("Starboard posts will now be removed when the messages star count is below the current star count.");
        else
            await ctx.Channel.SendConfirmAsync("Starboard posts will no longer be removed when the messages star count is below the current star count");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardAllowBots(bool enabled)
    {
        await Service.SetStarboardAllowBots(ctx.Guild, enabled);
        if (enabled)
            await ctx.Channel.SendConfirmAsync("Starboard will no longer ignore bots.");
        else
            await ctx.Channel.SendConfirmAsync("Starboard will now ignore bots.");
    }
}