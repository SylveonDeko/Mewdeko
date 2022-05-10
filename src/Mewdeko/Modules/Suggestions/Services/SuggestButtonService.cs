using Discord;
using Discord.Interactions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Modals;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Suggestions.Services;

public class SuggestButtonService : MewdekoSlashSubmodule<SuggestionsService>
{
    [ComponentInteraction("emotebutton:*")]
    public async Task UpdateCount(string number)
    {
        await DeferAsync(true);
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var componentData = ComponentBuilder.FromMessage(componentInteraction.Message);
        if (!int.TryParse(number, out var emoteNum)) return;
        var changed = false;
        var pickedEmote = Service.GetPickedEmote(componentInteraction.Message.Id, ctx.User.Id);
        if (pickedEmote == emoteNum)
        {
            if (await PromptUserConfirmAsync("Do you want to remove your vote?", ctx.User.Id, true, false))
            {
                changed = true;
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Vote removed.");
            }
            else
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync("Vote not removed.");
                return;
            }
        }
        if (pickedEmote != 0 && pickedEmote != emoteNum)
        {
            if (!await PromptUserConfirmAsync("Are you sure you wanna change your vote?", ctx.User.Id, true, false))
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync("Vote not changed.");
                return;
            }

            await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Vote changed!");
        }
        await Service.UpdatePickedEmote(componentInteraction.Message.Id, ctx.User.Id, changed ? 0 : emoteNum);
        var suggest = Service.GetSuggestByMessage(componentInteraction.Message.Id);
        var builder = new ComponentBuilder();
        var rows = componentData.ActionRows;
        var buttons = rows.ElementAt(0).Components;
        var count = 0;
        foreach (var i in buttons.Select(x => x as ButtonComponent))
        {
            ++count;
            var splitNum = int.Parse(i.CustomId.Split(":")[1]);
            if (splitNum == emoteNum && !changed)
            {
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, emoteNum);
                var label = int.Parse(i.Label);
                builder.WithButton((label+1).ToString(), $"emotebutton:{emoteNum}",
                    emote: Service.GetSuggestMote(ctx.Guild, emoteNum), style: Service.GetButtonStyle(ctx.Guild, emoteNum));
                continue;
            }
    
            if (splitNum == pickedEmote)
            {
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, splitNum, true);
                var label = int.Parse(i.Label);
                builder.WithButton((label-1).ToString(), $"emotebutton:{splitNum}",
                    emote: Service.GetSuggestMote(ctx.Guild, splitNum), style: Service.GetButtonStyle(ctx.Guild, splitNum));
                continue;
            }
            builder.WithButton(i.Label, 
                customId: $"emotebutton:{count}", Service.GetButtonStyle(ctx.Guild, count), Service.GetSuggestMote(ctx.Guild, count));
        }
        if (Service.GetThreadType(ctx.Guild) == 1)
        {
            builder.WithButton("Join/Create Public Discussion", customId: $"publicsuggestthread:{suggest.SuggestionId}", ButtonStyle.Secondary, row: 1);
        }
        if (Service.GetThreadType(ctx.Guild) == 2)
        {
            builder.WithButton("Join/Create Private Discussion", customId: $"privatesuggestthread:{suggest.SuggestionId}", ButtonStyle.Secondary, row: 1);
        }
        await componentInteraction.Message.ModifyAsync(x => x.Components = builder.Build());
    }

    [ComponentInteraction("publicsuggestthread:*")]
    public async Task PublicThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (Service.GetThreadType(ctx.Guild) is 0 or 2)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(Service.GetSuggestionChannel(ctx.Guild.Id));
        if (Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion", ThreadType.PublicThread, message: componentInteraction.Message);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId);
            if (user is not null)
                await threadChannel.AddUserAsync(user);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!");
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(Service.GetThreadByMessage(suggest.MessageId));
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}");
    }
    
    [ComponentInteraction("privatesuggestthread:*")]
    public async Task PrivateThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (Service.GetThreadType(ctx.Guild) is 0 or 1)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(Service.GetSuggestionChannel(ctx.Guild.Id));
        var a = Service.GetThreadByMessage(suggest.MessageId);
        if (Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion", ThreadType.PrivateThread, message: componentInteraction.Message);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId);
            if (user is not null)
                await threadChannel.AddUserAsync(user);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!");
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(Service.GetThreadByMessage(suggest.MessageId));
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}");
    }

    [ComponentInteraction("suggestbutton")]
    public async Task SendSuggestModal()
        => await ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
                        null,
                        x => x.UpdateTextInput("suggestion",
                            s => s.WithMaxLength(Math.Min(4000, Service.GetMaxLength(ctx.Guild?.Id)))
                                  .WithMinLength(Math.Min(Service.GetMinLength(ctx.Guild?.Id), 4000))))
                    .ConfigureAwait(false);
}