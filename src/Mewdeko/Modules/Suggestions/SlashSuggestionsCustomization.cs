using Discord;
using Discord.Interactions;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

public class SlashSuggestionsCustomization : MewdekoSlashModuleBase<SuggestionsService>
{
    [SlashCommand("suggestmessage", "Allows to set a custom embed when suggesting."), RequireContext(ContextType.Guild),
     RequireUserPermission(GuildPermission.Administrator)]
    public async Task SuggestMessage(string embed)
    {
        CrEmbed.TryParse(embed, out var crEmbed);
        if (embed == "-")
        {
            await Service.SetSuggestionMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Suggestions will now have the default look.");
            return;
        }

        if ((crEmbed is not null && !crEmbed.IsValid) || !embed.Contains("%suggest"))
        {
            await ctx.Interaction.SendErrorAsync(
                "The embed code you provided cannot be used for suggestion messages!");
            return;
        }

        await Service.SetSuggestionMessage(ctx.Guild, embed);
        var ebe = CrEmbed.TryParse(Service.GetSuggestionMessage(ctx.Guild), out crEmbed);
        if (ebe is false)
        {
            await Service.SetSuggestionMessage(ctx.Guild, "-");
            await ctx.Interaction.SendErrorAsync(
                "There was an error checking the embed, it may be invalid, so I set the suggest message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Sucessfully updated suggestion message!");
    }

    [SlashCommand("suggestminlength", "Set the minimum suggestion length."), RequireContext(ContextType.Guild),
     RequireUserPermission(GuildPermission.Administrator)]
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
     RequireUserPermission(GuildPermission.Administrator)]
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
     RequireUserPermission(GuildPermission.Administrator)]
    public async Task AcceptMessage(string embed)
    {
        CrEmbed.TryParse(embed, out var crEmbed);
        if (embed == "-")
        {
            await Service.SetAcceptMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Accepted Suggestions will now have the default look.");
            return;
        }

        if ((crEmbed is not null && !crEmbed.IsValid) || !embed.Contains("%suggest"))
        {
            await ctx.Interaction.SendErrorAsync(
                "The embed code you provided cannot be used for accepted suggestion messages!");
            return;
        }

        await Service.SetAcceptMessage(ctx.Guild, embed);
        var ebe = CrEmbed.TryParse(Service.GetAcceptMessage(ctx.Guild), out crEmbed);
        if (ebe is false)
        {
            await Service.SetAcceptMessage(ctx.Guild, "-");
            await ctx.Interaction.SendErrorAsync(
                "There was an error checking the embed, it may be invalid, so I set the accept message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Sucessfully updated accepted suggestion message!");
    }

    [SlashCommand("implementmessage", "Allows to set a custom embed when a suggestion is set implemented."), RequireContext(ContextType.Guild),
     RequireUserPermission(GuildPermission.Administrator)]
    public async Task ImplementMessage(string embed)
    {
        CrEmbed.TryParse(embed, out var crEmbed);
        if (embed == "-")
        {
            await Service.SetImplementMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Implemented Suggestions will now have the default look.");
            return;
        }

        if ((crEmbed is not null && !crEmbed.IsValid) || !embed.Contains("%suggest"))
        {
            await ctx.Interaction.SendErrorAsync(
                "The embed code you provided cannot be used for implemented suggestion messages!");
            return;
        }

        await Service.SetImplementMessage(ctx.Guild, embed);
        var ebe = CrEmbed.TryParse(Service.GetImplementMessage(ctx.Guild), out crEmbed);
        if (ebe is false)
        {
            await Service.SetImplementMessage(ctx.Guild, "-");
            await ctx.Interaction.SendErrorAsync(
                "There was an error checking the embed, it may be invalid, so I set the implemented message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
    }

    [SlashCommand("denymessage", "Allows to set a custom embed when a suggestion is denied."), RequireContext(ContextType.Guild),
     RequireUserPermission(GuildPermission.Administrator)]
    public async Task DenyMessage(string embed)
    {
        CrEmbed.TryParse(embed, out var crEmbed);
        if (embed == "-")
        {
            await Service.SetDenyMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Denied Suggestions will now have the default look.");
            return;
        }

        if ((crEmbed is not null && !crEmbed.IsValid) || !embed.Contains("%suggest"))
        {
            await ctx.Interaction.SendErrorAsync(
                "The embed code you provided cannot be used for denied suggestion messages!");
            return;
        }

        await Service.SetDenyMessage(ctx.Guild, embed);
        var ebe = CrEmbed.TryParse(Service.GetDenyMessage(ctx.Guild), out crEmbed);
        if (ebe is false)
        {
            await Service.SetDenyMessage(ctx.Guild, "-");
            await ctx.Interaction.SendErrorAsync(
                "There was an error checking the embed, it may be invalid, so I set the deny message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Sucessfully updated denied suggestion message!");
    }

    [SlashCommand("considermessage", "Allows to set a custom embed when a suggestion is considered."), RequireContext(ContextType.Guild),
     RequireUserPermission(GuildPermission.Administrator)]
    public async Task ConsiderMessage(string embed)
    {
        CrEmbed.TryParse(embed, out var crEmbed);
        if (embed == "-")
        {
            await Service.SetConsiderMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Considered Suggestions will now have the default look.");
            return;
        }

        if ((crEmbed is not null && !crEmbed.IsValid) || !embed.Contains("%suggest"))
        {
            await ctx.Interaction.SendErrorAsync(
                "The embed code you provided cannot be used for considered suggestion messages!");
            return;
        }

        await Service.SetConsiderMessage(ctx.Guild, embed);
        var ebe = CrEmbed.TryParse(Service.GetConsiderMessage(ctx.Guild), out crEmbed);
        if (ebe is false)
        {
            await Service.SetConsiderMessage(ctx.Guild, "-");
            await ctx.Interaction.SendErrorAsync(
                "There was an error checking the embed, it may be invalid, so I set the Consider message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
            return;
        }

        await ctx.Interaction.SendConfirmAsync("Sucessfully updated considered suggestion message!");
    }
}