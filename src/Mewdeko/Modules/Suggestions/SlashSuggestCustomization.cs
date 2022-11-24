using Discord.Interactions;
using Mewdeko.Modules.Suggestions.Services;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Suggestions;

[Group("suggestionscustomize", "Manage suggestions!")]
public class SlashSuggestionsCustomization : MewdekoSlashModuleBase<SuggestionsService>
{
    [SlashCommand("suggestmessage", "Allows to set a custom embed when suggesting."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Suggestions will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated suggestion message!").ConfigureAwait(false);
    }

    [SlashCommand("suggestminlength", "Set the minimum suggestion length."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task MinSuggestionLength(int length)
    {
        if (length >= 2048)
        {
            await ctx.Interaction.SendErrorAsync("Can't set this value because it means users will not be able to suggest anything!").ConfigureAwait(false);
            return;
        }

        await Service.SetMinLength(ctx.Guild, length).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Minimum length set to {length} characters!").ConfigureAwait(false);
    }

    [SlashCommand("suggestmaxlength", "Set the maximum suggestion length."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task MaxSuggestionLength(int length)
    {
        if (length <= 0)
        {
            await ctx.Interaction.SendErrorAsync("Cant set this value because it means users will not be able to suggest anything!").ConfigureAwait(false);
            return;
        }

        await Service.SetMaxLength(ctx.Guild, length).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Max length set to {length} characters!").ConfigureAwait(false);
    }

    [SlashCommand("acceptmessage", "Allows to set a custom embed when a suggestion is accepted."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AcceptMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Accepted Suggestions will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated accepted suggestion message!").ConfigureAwait(false);
    }

    [SlashCommand("implementmessage", "Allows to set a custom embed when a suggestion is set implemented."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ImplementMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Implemented Suggestions will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated implemented suggestion message!").ConfigureAwait(false);
    }

    [SlashCommand("denymessage", "Allows to set a custom embed when a suggestion is denied."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task DenyMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Denied Suggestions will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated denied suggestion message!").ConfigureAwait(false);
    }

    [SlashCommand("considermessage", "Allows to set a custom embed when a suggestion is considered."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ConsiderMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Considered Suggestions will now have the default look.").ConfigureAwait(false);
            return;
        }

        await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated considered suggestion message!").ConfigureAwait(false);
    }

    [SlashCommand("emotesmode", "Set whether suggestmotes are buttons or reactions"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMotesMode(Suggestions.SuggestEmoteModeEnum mode)
    {
        await Service.SetEmoteMode(ctx.Guild, (int)mode).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Sucessfully set Emote Mode to {mode}").ConfigureAwait(false);
    }

    [SlashCommand("buttoncolor", "Change the color of the suggestion button"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task SuggestButtonColor(Suggestions.ButtonType type)
    {
        await Service.SetSuggestButtonColor(ctx.Guild, (int)type).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Suggest Button Color will now be `{type}`").ConfigureAwait(false);
        await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild)).ConfigureAwait(false);
    }

    [SlashCommand("emotecolor", "Set the color of each button on a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMoteColor([Summary("number", "The number you want to change")] int num, Suggestions.ButtonType type)
    {
        await Service.SetButtonType(ctx.Guild, num, (int)type).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Suggest Button {num} will now be `{type}`").ConfigureAwait(false);
        await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild)).ConfigureAwait(false);
    }

    [SlashCommand("acceptchannel", "Set the channel accepted suggestions get sent to."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AcceptChannel(ITextChannel? channel = null)
    {
        await Service.SetAcceptChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Accept Channel Disabled.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"Accept channel set to {channel.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("denychannel", "Set the channel denied suggestions go to."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task DenyChannel(ITextChannel? channel = null)
    {
        await Service.SetDenyChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Deny Channel Disabled.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"Deny channel set to {channel.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("considerchannel", "Set the channel considered suggestions go to."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ConsiderChannel(ITextChannel? channel = null)
    {
        await Service.SetConsiderChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Consider Channel Disabled.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"Consider channel set to {channel.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("implementchannel", "Set the channel where implemented suggestions go"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ImplementChannel(ITextChannel? channel = null)
    {
        await Service.SetImplementChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Implement Channel Disabled.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"Implement channel set to {channel.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("threadstype", "Set the type of threads used in suggestions."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestThreadsType(Suggestions.SuggestThreadType type)
    {
        if (type == Suggestions.SuggestThreadType.Private && !ctx.Guild.Features.HasPrivateThreads)
        {
            await ctx.Interaction.SendErrorAsync("You do not have enough server boosts for private threads!").ConfigureAwait(false);
            return;
        }

        await Service.SetSuggestThreadsType(ctx.Guild, (int)type).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Succesfully set Suggestion Threads Type to `{type}`").ConfigureAwait(false);
    }

    [SlashCommand("archiveondeny", "Set whether threads auto archive on deny"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnDeny()
    {
        var current = await Service.GetArchiveOnDeny(ctx.Guild);
        await Service.SetArchiveOnDeny(ctx.Guild, !current).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Archive on deny is now set to `{!current}`").ConfigureAwait(false);
    }

    [SlashCommand("archiveonaccept", "Set whether threads auto archive on accept"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnAccept()
    {
        var current = await Service.GetArchiveOnAccept(ctx.Guild);
        await Service.SetArchiveOnAccept(ctx.Guild, !current).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Archive on accept is now set to `{!current}`").ConfigureAwait(false);
    }

    [SlashCommand("archiveonconsider", "Set whether threads auto archive on consider"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnConsider()
    {
        var current = await Service.GetArchiveOnConsider(ctx.Guild);
        await Service.SetArchiveOnConsider(ctx.Guild, !current).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Archive on consider is now set to `{!current}`").ConfigureAwait(false);
    }

    [SlashCommand("archiveonimplement", "Set whether threads auto archive on implement"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnImplement()
    {
        var current = await Service.GetArchiveOnImplement(ctx.Guild);
        await Service.SetArchiveOnImplement(ctx.Guild, !current).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Archive on implement is now set to `{!current}`").ConfigureAwait(false);
    }
}