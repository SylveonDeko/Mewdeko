using System.Globalization;
using Discord.Interactions;
using Mewdeko.Common.Configs;
using Mewdeko.Services.strings;

namespace Mewdeko.Common
{
    /// <summary>
    /// Base class for slash command modules in Mewdeko.
    /// </summary>
    public abstract class MewdekoSlashCommandModule : InteractionModuleBase
    {
        /// <summary>
        /// The culture information used for localization.
        /// </summary>
        protected CultureInfo? CultureInfo { get; set; }

        /// <summary>
        /// The bot strings service for localization.
        /// </summary>
        public IBotStrings? Strings { get; set; }

        /// <summary>
        /// The command handler service.
        /// </summary>
        public CommandHandler? CmdHandler { get; set; }

        /// <summary>
        /// The localization service.
        /// </summary>
        public ILocalization? Localization { get; set; }

        /// <summary>
        /// The bot configuration.
        /// </summary>
        public BotConfig Config { get; set; }

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Gets the interaction context.
        /// </summary>
        protected IInteractionContext ctx => Context;

        /// <summary>
        /// Executed before the command is executed.
        /// Sets the culture information based on the guild's localization settings.
        /// </summary>
        public override void BeforeExecute(ICommandInfo cmd) => CultureInfo = Localization.GetCultureInfo(ctx.Guild);

        /// <summary>
        /// Retrieves a localized text message for the given key.
        /// </summary>
        protected string? GetText(string? key) => Strings.GetText(key, CultureInfo);

        /// <summary>
        /// Retrieves a localized text message for the given key with optional arguments.
        /// </summary>
        protected string? GetText(string? key, params object?[] args) => Strings.GetText(key, CultureInfo, args);

        /// <summary>
        /// Sends an error message based on the specified key with optional arguments.
        /// </summary>
        public Task ErrorLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendErrorAsync(text, Config);
        }

        /// <summary>
        /// Sends an error message as a reply to the user with the specified key and optional arguments.
        /// </summary>
        public Task ReplyErrorLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config);
        }

        /// <summary>
        /// Sends an ephemeral error message as a reply to the user with the specified key and optional arguments.
        /// </summary>
        public Task EphemeralReplyErrorLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendEphemeralErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config);
        }

        /// <summary>
        /// Sends a confirmation message based on the specified key with optional arguments.
        /// </summary>
        public Task ConfirmLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendConfirmAsync(text);
        }

        /// <summary>
        /// Sends a confirmation message as a reply to the user with the specified key and optional arguments.
        /// </summary>
        public Task ReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
        }

        /// <summary>
        /// Sends an ephemeral confirmation message as a reply to the user with the specified key and optional arguments.
        /// </summary>
        public Task EphemeralReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Interaction.SendEphemeralConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
        }

        /// <summary>
        /// Prompts the user to confirm an action with the specified text and user ID.
        /// </summary>
        public Task<bool> PromptUserConfirmAsync(string text, ulong uid, bool ephemeral = false, bool delete = true) =>
            PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(text), uid, ephemeral, delete);

        /// <summary>
        /// Prompts the user to confirm an action with the specified embed, user ID, ephemeral status, and delete option.
        /// </summary>
        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid, bool ephemeral = false,
            bool delete = true)
        {
            embed.WithOkColor();
            var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
                .WithButton("No", "no", ButtonStyle.Danger);
            if (!ctx.Interaction.HasResponded) await ctx.Interaction.DeferAsync(ephemeral).ConfigureAwait(false);
            var msg = await ctx.Interaction
                .FollowupAsync(embed: embed.Build(), components: buttons.Build(), ephemeral: ephemeral)
                .ConfigureAwait(false);
            try
            {
                var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);
                return input == "Yes";
            }
            finally
            {
                if (delete)
                    _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        /// <summary>
        /// Checks the hierarchy of roles between the current user and the target user.
        /// </summary>
        public async Task<bool> CheckRoleHierarchy(IGuildUser target, bool displayError = true)
        {
            var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
            var ownerId = Context.Guild.OwnerId;
            var botMaxRole = curUser.GetRoles().Max(r => r.Position);
            var targetMaxRole = target.GetRoles().Max(r => r.Position);
            var modMaxRole = ((IGuildUser)ctx.User).GetRoles().Max(r => r.Position);

            var hierarchyCheck = ctx.User.Id == ownerId
                ? botMaxRole > targetMaxRole
                : botMaxRole >= targetMaxRole && modMaxRole > targetMaxRole;

            if (!hierarchyCheck && displayError)
                await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);

            return hierarchyCheck;
        }


        /// <summary>
        /// Gets the user's input from a button interaction.
        /// </summary>
        /// <param name="channelId">The channel ID to bind to</param>
        /// <param name="msgId">The message ID to bind to</param>
        /// <param name="userId">The user ID to bind to</param>
        /// <param name="alreadyDeferred">Whether the interaction was already responded to.</param>
        /// <returns></returns>
        public async Task<string>? GetButtonInputAsync(ulong channelId, ulong msgId, ulong userId,
            bool alreadyDeferred = false)
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
                            if (!alreadyDeferred) c.DeferAsync();
                            return Task.CompletedTask;
                        }

                        if (c.Data.CustomId == "yes")
                        {
                            if (!alreadyDeferred) c.DeferAsync();
                            userInputTask.TrySetResult("Yes");
                            return Task.CompletedTask;
                        }

                        if (!alreadyDeferred) c.DeferAsync();
                        userInputTask.TrySetResult(c.Data.CustomId);
                        return Task.CompletedTask;
                    });
                }

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Gets the user's input from a message.
        /// </summary>
        /// <param name="channelId">The channel ID to bind to.</param>
        /// <param name="userId">The user ID to bind to.</param>
        /// <returns></returns>
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

    /// <summary>
    /// Base class for generic slash command modules in Mewdeko.
    /// </summary>
    public abstract class MewdekoSlashModuleBase<TService> : MewdekoSlashCommandModule
    {
        /// <summary>
        /// The service associated with the module.
        /// </summary>
        public TService Service { get; set; }
    }

    /// <summary>
    /// Base class for generic slash submodule in Mewdeko.
    /// </summary>
    public abstract class MewdekoSlashSubmodule : MewdekoSlashCommandModule
    {
    }

    /// <summary>
    /// Base class for generic slash submodule with a service in Mewdeko.
    /// </summary>
    public abstract class MewdekoSlashSubmodule<TService> : MewdekoSlashModuleBase<TService>
    {
    }
}