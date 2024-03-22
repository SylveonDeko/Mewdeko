using System.Globalization;
using Discord.Commands;
using Mewdeko.Services.strings;

namespace Mewdeko.Common
{
    /// <summary>
    /// Base class for Discord command modules with support for localization and string resources.
    /// </summary>
    public abstract class MewdekoModule : ModuleBase
    {
        /// <summary>
        /// Gets or sets the CultureInfo for the current module.
        /// </summary>
        protected CultureInfo? CultureInfo { get; set; }

        /// <summary>
        /// Gets or sets the string resources provider for the current module.
        /// </summary>
        public IBotStrings? Strings { get; set; }

        /// <summary>
        /// Gets or sets the localization service for the current module.
        /// </summary>
        public ILocalization? Localization { get; set; }

        // ReSharper disable once InconsistentNaming
        protected ICommandContext ctx => Context;

        /// <summary>
        /// Performs tasks before executing a command.
        /// </summary>
        /// <param name="command">The command being executed.</param>
        protected override void BeforeExecute(CommandInfo command) =>
            CultureInfo = Localization.GetCultureInfo(ctx.Guild?.Id);

        /// <summary>
        /// Retrieves a localized text string using the specified key.
        /// </summary>
        /// <param name="key">The key identifying the text string.</param>
        /// <returns>The localized text string.</returns>
        protected string? GetText(string? key) => Strings.GetText(key, CultureInfo);

        /// <summary>
        /// Retrieves a formatted localized text string using the specified key and arguments.
        /// </summary>
        /// <param name="key">The key identifying the text string.</param>
        /// <param name="args">The arguments to format into the text string.</param>
        /// <returns>The formatted localized text string.</returns>
        protected string? GetText(string? key, params object?[] args) => Strings.GetText(key, CultureInfo, args);

        /// <summary>
        /// Sends an error message to the channel with localized text.
        /// </summary>
        /// <param name="textKey">The key identifying the text string.</param>
        /// <param name="args">The arguments to format into the text string.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<IUserMessage> ErrorLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendErrorAsync(text);
        }

        /// <summary>
        /// Sends an error message to the channel with localized text, mentioning the user who invoked the command.
        /// </summary>
        /// <param name="textKey">The key identifying the text string.</param>
        /// <param name="args">The arguments to format into the text string.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<IUserMessage> ReplyErrorLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}");
        }

        /// <summary>
        /// Sends a confirmation message to the channel with localized text.
        /// </summary>
        /// <param name="textKey">The key identifying the text string.</param>
        /// <param name="args">The arguments to format into the text string.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<IUserMessage> ConfirmLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendConfirmAsync(text);
        }

        /// <summary>
        /// Sends a confirmation message to the channel with localized text, mentioning the user who invoked the command.
        /// </summary>
        /// <param name="textKey">The key identifying the text string.</param>
        /// <param name="args">The arguments to format into the text string.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task<IUserMessage> ReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
        }

        /// <summary>
        /// Prompts the user with a confirmation message asynchronously.
        /// </summary>
        /// <param name="message">The message to be displayed to the user.</param>
        /// <param name="userid">The ID of the user to prompt.</param>
        /// <returns>A task representing the asynchronous operation, containing a boolean indicating the user's response.</returns>
        public Task<bool> PromptUserConfirmAsync(string message, ulong userid)
            => PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(message), userid);

        /// <summary>
        /// Prompts the user with a confirmation message asynchronously.
        /// </summary>
        /// <param name="embed">The embed to be displayed to the user.</param>
        /// <param name="userid">The ID of the user to prompt.</param>
        /// <returns>A task representing the asynchronous operation, containing a boolean indicating the user's response.</returns>
        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid)
        {
            embed.WithOkColor();
            var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
                .WithButton("No", "no", ButtonStyle.Danger);
            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), components: buttons.Build())
                .ConfigureAwait(false);
            try
            {
                var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);

                return input == "Yes";
            }
            finally
            {
                _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        /// <summary>
        /// Checks if the invoking user has a higher role hierarchy than the target user.
        /// </summary>
        /// <param name="target">The target user to check against.</param>
        /// <param name="displayError">Determines whether to display an error message if hierarchy check fails.</param>
        /// <returns>A task representing the asynchronous operation, containing a boolean indicating the result of the hierarchy check.</returns>
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
        /// Prompts the user with a confirmation message asynchronously using an existing message.
        /// </summary>
        /// <param name="message">The existing message to modify with the prompt.</param>
        /// <param name="embed">The embed to be displayed to the user.</param>
        /// <param name="userid">The ID of the user to prompt.</param>
        /// <returns>A task representing the asynchronous operation, containing a boolean indicating the user's response.</returns>
        public async Task<bool> PromptUserConfirmAsync(IUserMessage message, EmbedBuilder embed, ulong userid)
        {
            embed.WithOkColor();
            var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
                .WithButton("No", "no", ButtonStyle.Danger);
            await message.ModifyAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = buttons.Build();
            }).ConfigureAwait(false);
            var input = await GetButtonInputAsync(message.Channel.Id, message.Id, userid).ConfigureAwait(false);

            return input == "Yes";
        }

        /// <summary>
        /// Gets the user input from a button interaction asynchronously.
        /// </summary>
        /// <param name="channelId">The ID of the channel where the interaction occurred.</param>
        /// <param name="msgId">The ID of the message where the interaction occurred.</param>
        /// <param name="userId">The ID of the user who interacted with the button.</param>
        /// <param name="alreadyDeferred">Determines whether the interaction has already been deferred.</param>
        /// <returns>A task representing the asynchronous operation, containing the user input string.</returns>
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

            async Task Interaction(SocketInteraction arg)
            {
                if (arg is SocketMessageComponent c)
                {
                    await Task.Run(async () =>
                    {
                        if (c.Channel.Id != channelId || c.Message.Id != msgId || c.User.Id != userId)
                        {
                            if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                            return Task.CompletedTask;
                        }

                        if (c.Data.CustomId == "yes")
                        {
                            if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                            userInputTask.TrySetResult("Yes");
                            return Task.CompletedTask;
                        }

                        if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                        userInputTask.TrySetResult(c.Data.CustomId);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Gets the next message sent by the user asynchronously.
        /// </summary>
        /// <param name="channelId">The ID of the channel where the message is expected.</param>
        /// <param name="userId">The ID of the user whose message is awaited.</param>
        /// <returns>A task representing the asynchronous operation, containing the user's message content.</returns>
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
                return Task.Run(async () =>
                {
                    if (arg.Author.Id != userId || arg.Channel.Id != channelId) return;
                    userInputTask.TrySetResult(arg.Content);
                    try
                    {
                        await arg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        //Exclude
                    }
                });
            }
        }

        /// <summary>
        /// Gets the next message sent by the user asynchronously, including the message object.
        /// </summary>
        /// <param name="channelId">The ID of the channel where the message is expected.</param>
        /// <param name="userId">The ID of the user whose message is awaited.</param>
        /// <returns>A task representing the asynchronous operation, containing the user's message.</returns>
        public async Task<SocketMessage>? NextFullMessageAsync(ulong channelId, ulong userId)
        {
            var userInputTask = new TaskCompletionSource<SocketMessage>();
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
                return Task.Run(async () =>
                {
                    if (arg.Author.Id != userId || arg.Channel.Id != channelId) return;
                    userInputTask.TrySetResult(arg);
                    try
                    {
                        await arg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        //Exclude
                    }
                });
            }
        }
    }

    /// <summary>
    /// Base class for Discord command modules with a specified service type and support for localization and string resources.
    /// </summary>
    /// <typeparam name="TService">The type of service associated with the module.</typeparam>
    public abstract class MewdekoModuleBase<TService> : MewdekoModule
    {
        /// <summary>
        /// Gets or sets the service instance associated with the module.
        /// </summary>
        public TService Service { get; set; }
    }

    /// <summary>
    /// Base class for submodules of Discord command modules.
    /// </summary>
    public abstract class MewdekoSubmodule : MewdekoModule
    {
    }

    /// <summary>
    /// Base class for submodules of Discord command modules with a specified service type.
    /// </summary>
    /// <typeparam name="TService">The type of service associated with the submodule.</typeparam>
    public abstract class MewdekoSubmodule<TService> : MewdekoModuleBase<TService>
    {
    }
}