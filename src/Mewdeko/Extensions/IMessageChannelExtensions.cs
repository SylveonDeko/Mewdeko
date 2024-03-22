using Discord.Commands;

namespace Mewdeko.Extensions;

/// <summary>
/// Extensions for IMessageChannel objects. Used a lot throughout the bot.
/// </summary>
public static class MessageChannelExtensions
{
    /// <summary>
    /// Sends an embed message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="embed">The embed builder containing the embed message.</param>
    /// <param name="msg">The optional message content to include.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> EmbedAsync(this IMessageChannel ch, EmbedBuilder embed, string? msg = "") =>
        ch.SendMessageAsync(msg, embed: embed.Build(),
            options: new RequestOptions
            {
                RetryMode = RetryMode.AlwaysRetry
            });

    /// <summary>
    /// Sends an error message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="title">The optional title of the error message.</param>
    /// <param name="error">The error message content.</param>
    /// <param name="url">The optional URL to include in the error message.</param>
    /// <param name="footer">The optional footer text for the error message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string? title, string? error,
        string? url = null, string? footer = null)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(error)
            .WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        if (!string.IsNullOrWhiteSpace(footer))
            eb.WithFooter(efb => efb.WithText(footer));
        return ch.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    /// Sends an error message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="error">The error message content.</param>
    /// <param name="helpButton">A flag indicating whether to include a support server button.</param>
    /// <param name="fields">Optional fields to include in the error message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendErrorAsync(
        this IMessageChannel ch,
        string? error,
        bool helpButton = true,
        EmbedFieldBuilder[]? fields = null)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(error);
        if (fields is not null)
            eb.WithFields(fields);
        return ch.SendMessageAsync(embed: eb.Build(),
            components: helpButton
                ? new ComponentBuilder().WithButton(label: "Support Server", style: ButtonStyle.Link,
                    url: "https://discord.gg/mewdeko").Build()
                : null);
    }


    /// <summary>
    /// Sends a confirmation message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="title">The optional title of the confirmation message.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <param name="url">The optional URL to include in the confirmation message.</param>
    /// <param name="footer">The optional footer text for the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string? title, string? text,
        string? url = null, string? footer = null)
    {
        var eb = new EmbedBuilder().WithOkColor().WithDescription(text)
            .WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        if (!string.IsNullOrWhiteSpace(footer))
            eb.WithFooter(efb => efb.WithText(footer));
        return ch.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    /// Sends a confirmation message to the interaction asynchronously.
    /// </summary>
    /// <param name="ch">The interaction to send the message to.</param>
    /// <param name="title">The optional title of the confirmation message.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <param name="url">The optional URL to include in the confirmation message.</param>
    /// <param name="footer">The optional footer text for the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmAsync(this IDiscordInteraction ch, string? title, string? text,
        string? url = null, string? footer = null)
    {
        var eb = new EmbedBuilder().WithOkColor().WithDescription(text)
            .WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        if (!string.IsNullOrWhiteSpace(footer))
            eb.WithFooter(efb => efb.WithText(footer));
        return ch.FollowupAsync(embed: eb.Build());
    }

    /// <summary>
    /// Sends a confirmation message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string? text) =>
        ch.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());

    /// <summary>
    /// Sends a confirmation reply message to the original message asynchronously.
    /// </summary>
    /// <param name="msg">The original message to reply to.</param>
    /// <param name="text">The text content of the confirmation reply message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmReplyAsync(this IUserMessage msg, string? text) =>
        msg.ReplyAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());

    /// <summary>
    /// Sends a confirmation message to the webhook client asynchronously.
    /// </summary>
    /// <param name="msg">The webhook client to send the message to.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation, returning the ID of the sent message.</returns>
    public static Task<ulong> SendConfirmAsync(this DiscordWebhookClient msg, string? text) =>
        msg.SendMessageAsync(embeds: new[]
        {
            new EmbedBuilder().WithOkColor().WithDescription(text).Build()
        });


    /// <summary>
    /// Sends an error reply message to the original message asynchronously.
    /// </summary>
    /// <param name="msg">The original message to reply to.</param>
    /// <param name="text">The text content of the error reply message.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendErrorReplyAsync(this IUserMessage msg, string? text) =>
        msg.ReplyAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(text).Build());

    /// <summary>
    /// Sends a confirmation message to the text channel asynchronously.
    /// </summary>
    /// <param name="ch">The text channel to send the message to.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <param name="builder">The optional component builder for interactive components.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmAsync(this ITextChannel ch, string? text,
        ComponentBuilder? builder = null) =>
        ch.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build(),
            components: builder?.Build());

    /// <summary>
    /// Sends a confirmation message to the message channel asynchronously.
    /// </summary>
    /// <param name="ch">The message channel to send the message to.</param>
    /// <param name="text">The text content of the confirmation message.</param>
    /// <param name="builder">The optional component builder for interactive components.</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string? text,
        ComponentBuilder? builder = null) =>
        ch.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build(),
            components: builder?.Build());


    /// <summary>
    /// Sends a table message asynchronously to the message channel.
    /// </summary>
    /// <typeparam name="T">The type of items in the table.</typeparam>
    /// <param name="ch">The message channel to send the table message to.</param>
    /// <param name="seed">The seed text for the table message.</param>
    /// <param name="items">The items to display in the table.</param>
    /// <param name="howToPrint">The function that defines how to print each item.</param>
    /// <param name="columns">The number of columns in the table (default is 3).</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, string seed, IEnumerable<T> items,
        Func<T, string> howToPrint, int columns = 3)
    {
        var i = 0;
        return ch.SendMessageAsync($@"{seed}```css
{string.Join("\n", items.GroupBy(_ => i++ / columns)
    .Select(ig => string.Concat(ig.Select(howToPrint))))}
```");
    }

    /// <summary>
    /// Sends a table message asynchronously to the message channel.
    /// </summary>
    /// <typeparam name="T">The type of items in the table.</typeparam>
    /// <param name="ch">The message channel to send the table message to.</param>
    /// <param name="items">The items to display in the table.</param>
    /// <param name="howToPrint">The function that defines how to print each item.</param>
    /// <param name="columns">The number of columns in the table (default is 3).</param>
    /// <returns>A task representing the asynchronous operation, returning the sent user message.</returns>
    public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, IEnumerable<T> items,
        Func<T, string> howToPrint, int columns = 3) =>
        ch.SendTableAsync("", items, howToPrint, columns);

    /// <summary>
    /// Adds a checkmark reaction to the message asynchronously.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task OkAsync(this ICommandContext ctx) => ctx.Message.AddReactionAsync(new Emoji("✅"));

    /// <summary>
    /// Adds a cross mark reaction to the message asynchronously.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task ErrorAsync(this ICommandContext ctx) => ctx.Message.AddReactionAsync(new Emoji("❌"));

    /// <summary>
    /// Adds a warning sign reaction to the message asynchronously.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task WarningAsync(this ICommandContext ctx) => ctx.Message.AddReactionAsync(new Emoji("⚠"));
}