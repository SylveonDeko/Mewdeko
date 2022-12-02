using System.Globalization;
using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Services.strings;

namespace Mewdeko.Common;

public abstract class MewdekoSlashCommandModule : InteractionModuleBase
{
    protected CultureInfo? CultureInfo { get; set; }
    public IBotStrings Strings { get; set; }
    public CommandHandler CmdHandler { get; set; }
    public ILocalization Localization { get; set; }

    // ReSharper disable once InconsistentNaming
    protected IInteractionContext ctx => Context;

    public override void BeforeExecute(ICommandInfo cmd) => CultureInfo = Localization.GetCultureInfo(ctx.Guild);

    protected string? GetText(string? key) => Strings.GetText(key, CultureInfo);

    protected string? GetText(string? key, params object?[] args) => Strings.GetText(key, CultureInfo, args);

    public async Task ErrorLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        await ctx.Interaction.SendErrorAsync(text).ConfigureAwait(false);
    }

    public async Task ReplyErrorLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        await ctx.Interaction.SendErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}").ConfigureAwait(false);
    }

    public async Task EphemeralReplyErrorLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        await ctx.Interaction.SendEphemeralErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}").ConfigureAwait(false);
    }

    public async Task ConfirmLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        await ctx.Interaction.SendConfirmAsync(text).ConfigureAwait(false);
    }

    public Task ReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Interaction.SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    public Task EphemeralReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Interaction.SendEphemeralConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    public async Task<bool> PromptUserConfirmAsync(string text, ulong uid, bool ephemeral = false, bool delete = true) =>
        await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(text), uid, ephemeral, delete).ConfigureAwait(false);

    public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid, bool ephemeral = false, bool delete = true)
    {
        embed.WithOkColor();
        var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
            .WithButton("No", "no", ButtonStyle.Danger);
        if (!ctx.Interaction.HasResponded) await ctx.Interaction.DeferAsync(ephemeral).ConfigureAwait(false);
        var msg = await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: buttons.Build(), ephemeral: ephemeral)
            .ConfigureAwait(false);
        try
        {
            var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);

            return input == "Yes";
        }
        finally
        {
            if (delete)
                _ = Task.Run(async () => await msg.DeleteAsync().ConfigureAwait(false));
        }
    }

    public async Task<bool> CheckRoleHierarchy(IGuildUser target, bool displayError = true)
    {
        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        var ownerId = Context.Guild.OwnerId;
        var modMaxRole = ((IGuildUser)ctx.User).GetRoles().Max(r => r.Position);
        var targetMaxRole = target.GetRoles().Max(r => r.Position);
        var botMaxRole = curUser.GetRoles().Max(r => r.Position);
        // bot can't punish a user who is higher in the hierarchy. Discord will return 403
        // moderator can be owner, in which case role hierarchy doesn't matter
        // otherwise, moderator has to have a higher role
        if (botMaxRole > targetMaxRole
            && (Context.User.Id == ownerId || targetMaxRole < modMaxRole)
            && target.Id != ownerId)
        {
            return true;
        }

        if (displayError)
            await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
        return false;
    }


    public async Task<string>? GetButtonInputAsync(ulong channelId, ulong msgId, ulong userId)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = (DiscordSocketClient)ctx.Client;
        try
        {
            dsc.InteractionCreated += Interaction;
            if (await Task.WhenAny(userInputTask.Task, Task.Delay(30000)).ConfigureAwait(false) !=
                userInputTask.Task)
            {
                return null;
            }

            return await userInputTask.Task.ConfigureAwait(false);
        }
        finally
        {
            dsc.InteractionCreated -= Interaction;
        }

        Task Interaction(SocketInteraction arg)
        {
            if (arg is SocketMessageComponent c)
            {
                Task.Run(() =>
                {
                    if (c.Channel.Id != channelId || c.Message.Id != msgId || c.User.Id != userId)
                    {
                        c.DeferAsync();
                        return Task.CompletedTask;
                    }

                    if (c.Data.CustomId == "yes")
                    {
                        c.DeferAsync();
                        userInputTask.TrySetResult("Yes");
                        return Task.CompletedTask;
                    }

                    c.DeferAsync();
                    userInputTask.TrySetResult(c.Data.CustomId);
                    return Task.CompletedTask;
                });
            }

            return Task.CompletedTask;
        }
    }

    public async Task<string>? NextMessageAsync(ulong channelId, ulong userId)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = (DiscordSocketClient)ctx.Client;
        try
        {
            dsc.MessageReceived += Interaction;
            if (await Task.WhenAny(userInputTask.Task, Task.Delay(60000)).ConfigureAwait(false) !=
                userInputTask.Task)
            {
                return null;
            }

            return await userInputTask.Task.ConfigureAwait(false);
        }
        finally
        {
            dsc.MessageReceived -= Interaction;
        }

        Task Interaction(SocketMessage arg)
        {
            Task.Run(() =>
            {
                if (arg.Author.Id != userId || arg.Channel.Id != channelId) return Task.CompletedTask;
                userInputTask.TrySetResult(arg.Content);
                try
                {
                    arg.DeleteAsync();
                }
                catch
                {
                    //Exclude
                }

                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        }
    }
}

public abstract class MewdekoSlashModuleBase<TService> : MewdekoSlashCommandModule
{
    public TService Service { get; set; }
}

public abstract class MewdekoSlashSubmodule : MewdekoSlashCommandModule
{
}

public abstract class MewdekoSlashSubmodule<TService> : MewdekoSlashModuleBase<TService>
{
}