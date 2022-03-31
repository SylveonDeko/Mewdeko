using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;
[Group("suggestions", "Send or manage suggestions!")]
public class SlashSuggestions : MewdekoSlashModuleBase<SuggestionsService>
{
    [SlashCommand("setchannel", "Sets the suggestion channel."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions, BlacklistCheck]
    public async Task SetSuggestChannel(ITextChannel? channel = null)
    {
        if (channel == null)
        {
            await Service.SetSuggestionChannelId(ctx.Guild, 0);
            await ctx.Interaction.SendConfirmAsync("Suggestions Disabled!");
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id);
            var chn2 = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
            await ctx.Interaction.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}");
        }
    }

    [SlashCommand("suggest", "Sends a suggestion to the suggestion channel, if there is one set."), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
    public async Task Suggest(string suggestion)
    {
        if (suggestion.Length > Service.GetMaxLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralConfirmAsync(
                $"Cannot send this suggestion as its over the max length (`{Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }
        if (suggestion.Length < Service.GetMinLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(
                $"Cannot send this suggestion as its under the minimum length (`{Service.GetMinLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            suggestion, ctx.Channel as ITextChannel, ctx.Interaction);
    }

    [SlashCommand("deny", "Denies a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions, BlacklistCheck]
    public async Task Deny([Summary(description:"The number of the suggestion.")]ulong suggestid, string? reason = null) =>
        await Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("accept", "Accepts a suggestion"),RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions, BlacklistCheck]
    public async Task Accept([Summary(description:"The number of the suggestion.")]ulong suggestid, string? reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("implement", "Sets a suggestion as implemented"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions, BlacklistCheck]
    public async Task Implemented([Summary(description:"The number of the suggestion.")]ulong suggestid, string? reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("consider", "Sets a suggestion as considered"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions, BlacklistCheck]
    public async Task Consider([Summary(description:"The number of the suggestion.")]ulong suggestid, string? reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);
    [SlashCommand("suggestmessage", "Allows to set a custom embed when suggesting."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task SuggestMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetSuggestionMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Suggestions will now have the default look.");
            return;
        }
        await Service.SetSuggestionMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated suggestion message!");
    }

    [SlashCommand("suggestminlength", "Set the minimum suggestion length."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MinSuggestionLength(int length)
    {
        if (length >= 2048)
        {
            await ctx.Interaction.SendErrorAsync(
                "Can't set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMinLength(ctx.Guild, length);
        await ctx.Interaction.SendConfirmAsync($"Minimum length set to {length} characters!");
    }
    
    [SlashCommand("suggestmaxlength", "Set the maximum suggestion length."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MaxSuggestionLength(int length)
    {
        if (length <= 0)
        {
            await ctx.Interaction.SendErrorAsync(
                "Cant set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMaxLength(ctx.Guild, length);
        await ctx.Interaction.SendConfirmAsync($"Max length set to {length} characters!");
    }
    
    [SlashCommand("acceptmessage", "Allows to set a custom embed when a suggestion is accepted."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task AcceptMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetAcceptMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Accepted Suggestions will now have the default look.");
            return;
        }
        await Service.SetAcceptMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated accepted suggestion message!");
    }

    [SlashCommand("implementmessage", "Allows to set a custom embed when a suggestion is set implemented."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task ImplementMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetImplementMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Implemented Suggestions will now have the default look.");
            return;
        }
        await Service.SetImplementMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
    }

    [SlashCommand("denymessage", "Allows to set a custom embed when a suggestion is denied."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task DenyMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetDenyMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Denied Suggestions will now have the default look.");
            return;
        }
        await Service.SetDenyMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated denied suggestion message!");
    }

    [SlashCommand("considermessage", "Allows to set a custom embed when a suggestion is considered."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task ConsiderMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetConsiderMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Considered Suggestions will now have the default look.");
            return;
        }
        await Service.SetConsiderMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated considered suggestion message!");
    }
}