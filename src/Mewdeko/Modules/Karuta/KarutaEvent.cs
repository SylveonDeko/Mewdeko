using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Karuta.Services;
using Serilog;
using System.Threading.Tasks;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Karuta;

public class KarutaEvent : MewdekoModuleBase<ShibaKarutaService>
{
    private readonly DbService _db;

    public KarutaEvent(DbService db) => _db = db;

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task KarutaEventChannel(ITextChannel channel = null)
    {
        if (channel is null)
        {
            var eventChannelId = await Service.GetEventChannel(ctx.Guild.Id);
            if (eventChannelId is 0)
            {
                await ctx.Channel.SendErrorAsync("No Event Channel set! Set one using this command with a channel mentioned.");
            }
            else
            {
                var eventChannel = await ctx.Guild.GetTextChannelAsync(eventChannelId);
                await ctx.Channel.SendConfirmAsync($"Your current Event Channel is {eventChannel.Mention}.");
            }
        }
        else
        {
            await Service.SetEventChannel(ctx.Guild.Id, channel.Id);
            await ctx.Channel.SendConfirmAsync($"Event Channel set to {channel.Mention}.");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task KarutaButtonText(int buttonNumber, [Remainder] string text = null)
    {
        if (buttonNumber is > 6 or < 1)
        {
            await ctx.Channel.SendErrorAsync("Number must be between 1 and 6.");
            return;
        }
            
        if (text is null)
        {
            var buttonText = await Service.GetButtonText(ctx.Guild.Id, buttonNumber);
            if (string.IsNullOrEmpty(buttonText))
            {
                await ctx.Channel.SendErrorAsync($"Button {buttonNumber} does not have any text set. Use this command with text to set it.");
            }
            else
            {
                await ctx.Channel.SendConfirmAsync($"The text for Button {buttonNumber} is:\n{buttonText}");
            }
        }
        else
        {
            await Service.SetButtonText(ctx.Guild.Id, buttonNumber, text);
            await ctx.Channel.SendConfirmAsync($"Text for Button {buttonNumber} has been set to\n{text}");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task RemoveEventEntry(int eventNum)
    {
        if (!await PromptUserConfirmAsync("Are you absolutely sure you want to remove this entry? This deletes it and the votes for it.", ctx.User.Id))
            return;
        var success = await Service.RemoveEntry(ctx.Guild.Id, eventNum);
        if (success)
            await ctx.Channel.SendConfirmAsync("Entry along with it's votes have been removed, and the message has been deleted.");
        else
            await ctx.Channel.SendErrorAsync("Looks like that entry doesn't exist. Please make sure you have the correct number.");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task ClearKarutaEvent()
    {
        if (await PromptUserConfirmAsync("Are you absolutely sure you want to clear all entries and votes?? This cannot be undone!", ctx.User.Id))
        {
            await Service.ClearEntries(ctx.Guild.Id);
            await ctx.Channel.SendConfirmAsync("All votes and entries have been cleared.");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages)]
    public async Task KarutaEventLeaderboard(int questionNum = 0)
    {
        var guildId = ctx.Guild.Id;
        await using var uow = _db.GetDbContext();
        if (questionNum is 0)
        {
            var embeds = new List<Embed>();
            for (var i = 1; i < 7; i++)
            {
                Log.Information($"{guildId} | {i}");
                    var embed = await Service.GetLeaderboardEmbed(guildId, i);
                    if (embed is not null)
                        embeds.Add(embed);
            }

            await ctx.Channel.SendMessageAsync(embeds: embeds.ToArray());
        }
        else
        {
            if (questionNum is > 6 or < 1)
            {
                await ctx.Channel.SendErrorAsync("Number must be between 1 and 6.");
                return;
            }
            var embed = await Service.GetLeaderboardEmbed(ctx.Guild.Id, questionNum);
            if (embed is null)
                await ctx.Channel.SendErrorAsync("Seems like that question hasn't been set. Please set it or try a different question number.");
            else
                await ctx.Channel.SendMessageAsync(embed: embed);
        }
    }

    
}