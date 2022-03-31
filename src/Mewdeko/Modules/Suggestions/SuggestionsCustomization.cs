using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

[Group]
public class SuggestionsCustomization : MewdekoModuleBase<SuggestionsService>
{
    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SuggestMessage([Remainder] string embed)
    {
        if (embed == "-")
        {
            await Service.SetSuggestionMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Suggestions will now have the default look.");
            return;
        }
        await Service.SetSuggestionMessage(ctx.Guild, embed);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated suggestion message!");
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task MinSuggestionLength(int length)
    {
        if (length >= 2048)
        {
            await ctx.Channel.SendErrorAsync(
                "Can't set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMinLength(ctx.Guild, length);
        await ctx.Channel.SendConfirmAsync($"Minimum length set to {length} characters!");
    }
    
    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task MaxSuggestionLength(int length)
    {
        if (length <= 0)
        {
            await ctx.Channel.SendErrorAsync(
                "Cant set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMaxLength(ctx.Guild, length);
        await ctx.Channel.SendConfirmAsync($"Max length set to {length} characters!");
    }
    
    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task AcceptMessage([Remainder] string embed)
    {
        if (embed == "-")
        {
            await Service.SetAcceptMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Accpeted Suggestions will now have the default look.");
            return;
        }
        await Service.SetAcceptMessage(ctx.Guild, embed);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated accpeted suggestion message!");
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task ImplementMessage([Remainder] string embed)
    {
        if (embed == "-")
        {
            await Service.SetImplementMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Implemented Suggestions will now have the default look.");
            return;
        }
        await Service.SetImplementMessage(ctx.Guild, embed);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task DenyMessage([Remainder] string embed)
    {
        if (embed == "-")
        {
            await Service.SetDenyMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Denied Suggestions will now have the default look.");
            return;
        }
        await Service.SetDenyMessage(ctx.Guild, embed);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated denied suggestion message!");
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task ConsiderMessage([Remainder] string embed)
    {
        if (embed == "-")
        {
            await Service.SetConsiderMessage(ctx.Guild, embed);
            await ctx.Channel.SendConfirmAsync("Suggestions will now have the default look.");
            return;
        }
        await Service.SetConsiderMessage(ctx.Guild, embed);
        await ctx.Channel.SendConfirmAsync("Sucessfully updated suggestion message!");
    }

    [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task SuggestMotes([Remainder] string? _ = null)
    {
        if (_ == null)
        {
            await ctx.Channel.SendErrorAsync(
                "You need to either provide emojis or say disable for this to work!");
            return;
        }

        if (_ != null && _.Contains("disable"))
        {
            await Service.SetSuggestionEmotes(ctx.Guild, "disable");
            await ctx.Channel.SendConfirmAsync("Disabled Custom Emotes for Suggestions");
            return;
        }

        if (_ != null && !_.Contains("disable") && ctx.Message.Tags.Where(t => t.Type == TagType.Emoji)
                .Select(x => (Emote) x.Value).Count() > 5)
        {
            await ctx.Channel.SendErrorAsync("You may only have up to 5 emotes for suggestions!");
            return;
        }

        if (!_.Contains("disable") && !ctx.Message.Tags.Where(t => t.Type == TagType.Emoji)
                .Select(x => (IEmote)x.Value).Any())
        {
            await ctx.Channel.SendErrorAsync("You need to specify up to 5 emotes for this command to work!");
            return;
        }

        var emotes = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (IEmote)x.Value);
        foreach (var emoji in emotes)
            try
            {
                await ctx.Message.AddReactionAsync(emoji);
            }
            catch
            {
                await ctx.Channel.SendErrorAsync($"Unable to access the emote {emoji.Name}, please add me to the server it's in or use a different emote.");
            }

        var list = emotes.Select(emote => emote.ToString()).ToList();
        await Service.SetSuggestionEmotes(ctx.Guild, string.Join(",", list));
        await ctx.Channel.SendConfirmAsync($"Suggestions will now be reacted with {string.Join(",", list)}");
    }
}