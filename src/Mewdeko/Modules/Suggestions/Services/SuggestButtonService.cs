using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Common.Modals;

namespace Mewdeko.Modules.Suggestions.Services;

public class SuggestButtonService : MewdekoSlashSubmodule<SuggestionsService>
{
    [ComponentInteraction("emotebutton:*")]
    public async Task UpdateCount(string number)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var componentData = ComponentBuilder.FromMessage(componentInteraction.Message);
        if (!int.TryParse(number, out var emoteNum)) return;
        var changed = false;
        var pickedEmote = await Service.GetPickedEmote(componentInteraction.Message.Id, ctx.User.Id);
        if (pickedEmote == emoteNum)
        {
            if (await PromptUserConfirmAsync("Do you want to remove your vote?", ctx.User.Id, true, false).ConfigureAwait(false))
            {
                changed = true;
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Vote removed.").ConfigureAwait(false);
            }
            else
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync("Vote not removed.").ConfigureAwait(false);
                return;
            }
        }

        if (pickedEmote != 0 && pickedEmote != emoteNum)
        {
            if (!await PromptUserConfirmAsync("Are you sure you wanna change your vote?", ctx.User.Id, true, false).ConfigureAwait(false))
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync("Vote not changed.").ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Vote changed!").ConfigureAwait(false);
        }

        await Service.UpdatePickedEmote(componentInteraction.Message.Id, ctx.User.Id, changed ? 0 : emoteNum).ConfigureAwait(false);
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
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
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, emoteNum).ConfigureAwait(false);
                var label = int.Parse(i.Label);
                builder.WithButton((label + 1).ToString(), $"emotebutton:{emoteNum}",
                    emote: await Service.GetSuggestMote(ctx.Guild, emoteNum), style: await Service.GetButtonStyle(ctx.Guild, emoteNum));
                continue;
            }

            if (splitNum == pickedEmote)
            {
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, splitNum, true).ConfigureAwait(false);
                var label = int.Parse(i.Label);
                builder.WithButton((label - 1).ToString(), $"emotebutton:{splitNum}",
                    emote: await Service.GetSuggestMote(ctx.Guild, splitNum), style: await Service.GetButtonStyle(ctx.Guild, splitNum));
                continue;
            }

            builder.WithButton(i.Label,
                customId: $"emotebutton:{count}", await Service.GetButtonStyle(ctx.Guild, count), await Service.GetSuggestMote(ctx.Guild, count));
        }

        if (await Service.GetThreadType(ctx.Guild) == 1)
        {
            builder.WithButton("Join/Create Public Discussion", customId: $"publicsuggestthread:{suggest.SuggestionId}", ButtonStyle.Secondary, row: 1);
        }

        if (await Service.GetThreadType(ctx.Guild) == 2)
        {
            builder.WithButton("Join/Create Private Discussion", customId: $"privatesuggestthread:{suggest.SuggestionId}", ButtonStyle.Secondary, row: 1);
        }

        await componentInteraction.Message.ModifyAsync(x => x.Components = builder.Build()).ConfigureAwait(false);
    }

    [ComponentInteraction("publicsuggestthread:*")]
    public async Task PublicThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (await Service.GetThreadType(ctx.Guild) is 0 or 2)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id)).ConfigureAwait(false);
        if (await Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion", ThreadType.PublicThread, message: componentInteraction.Message)
                .ConfigureAwait(false);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            if (user is not null)
                await threadChannel.AddUserAsync(user).ConfigureAwait(false);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser).ConfigureAwait(false);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id).ConfigureAwait(false);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!").ConfigureAwait(false);
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(await Service.GetThreadByMessage(suggest.MessageId)).ConfigureAwait(false);
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}").ConfigureAwait(false);
    }

    [ComponentInteraction("privatesuggestthread:*")]
    public async Task PrivateThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (await Service.GetThreadType(ctx.Guild) is 0 or 1)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id)).ConfigureAwait(false);
        if (await Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion", ThreadType.PrivateThread, message: componentInteraction.Message)
                .ConfigureAwait(false);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            if (user is not null)
                await threadChannel.AddUserAsync(user).ConfigureAwait(false);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser).ConfigureAwait(false);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id).ConfigureAwait(false);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!").ConfigureAwait(false);
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(await Service.GetThreadByMessage(suggest.MessageId)).ConfigureAwait(false);
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}").ConfigureAwait(false);
    }

    [ComponentInteraction("suggestbutton")]
    public async Task SendSuggestModal()
        => await ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
            null,
            x => x.UpdateTextInput("suggestion", async s
                => s.WithMaxLength(Math.Min(4000, await Service.GetMaxLength(ctx.Guild.Id)))
                    .WithMinLength(Math.Min(await Service.GetMinLength(ctx.Guild.Id), 4000)))).ConfigureAwait(false);
}