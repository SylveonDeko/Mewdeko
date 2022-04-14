using Discord;
using Discord.Interactions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;
[Group("suggestionscustomize", "Manage suggestions!")]
public class SlashSuggestionsCustomization : MewdekoSlashModuleBase<SuggestionsService>
{
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