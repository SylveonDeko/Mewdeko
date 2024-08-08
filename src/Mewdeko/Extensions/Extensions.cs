#nullable enable
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Discord.Commands;
using Fergun.Interactive;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Configs;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using ModuleInfo = Discord.Commands.ModuleInfo;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko.Extensions;

/// <summary>
/// Most of the extension methods for Mewdeko.
/// </summary>
public static partial class Extensions
{


    /// <summary>
    /// Regular expression for matching URLs.
    /// </summary>
    public static readonly Regex UrlRegex = MyRegex();

    /// <summary>
    /// Maps each element in the input array to an output array using the provided function.
    /// </summary>
    /// <typeparam name="TIn">Input array element type.</typeparam>
    /// <typeparam name="TOut">Output array element type.</typeparam>
    /// <param name="arr">Input array.</param>
    /// <param name="f">Mapping function.</param>
    /// <returns>Mapped array.</returns>
    public static TOut[] Map<TIn, TOut>(this TIn[] arr, Func<TIn, TOut> f) => Array.ConvertAll(arr, x => f(x));

    /// <summary>
    /// Get Scoped Service
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="service"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IServiceScope GetScopedService<T>(this IServiceScopeFactory scopeFactory, out T service) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(scopeFactory, nameof(scopeFactory));

        var scope = scopeFactory.CreateScope();
        service = scope.ServiceProvider.GetRequiredService<T>();

        return scope;
    }

    /// <summary>
    /// Sends a confirmation message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the confirmation.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task SendConfirmAsync(this IDiscordInteraction interaction, string? message)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build());

    /// <summary>
    /// Sends a confirmation message asynchronously with ephemeral visibility.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the confirmation.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task SendEphemeralConfirmAsync(this IDiscordInteraction interaction, string message)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(),
            ephemeral: true);

    /// <summary>
    /// Sends an error message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the error.</param>
    /// /// <param name="config">Bot configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task SendErrorAsync(this IDiscordInteraction interaction, string? message, BotConfig config)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            components: config.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                    .WithButton(label: "Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                    .Build()
                : null);

    /// <summary>
    /// Sends an error message asynchronously with ephemeral visibility.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the error.</param>
    /// /// <param name="config">Bot configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task SendEphemeralErrorAsync(this IDiscordInteraction interaction, string? message, BotConfig config)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            ephemeral: true, components: config.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                    .WithButton(label: "Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                    .Build()
                : null);

    /// <summary>
    /// Sends a confirmation follow-up message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the follow-up.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task<IUserMessage> SendConfirmFollowupAsync(this IDiscordInteraction interaction,
        string message)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build());

    /// <summary>
    /// Sends a confirmation follow-up message asynchronously with a custom component builder.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the follow-up.</param>
    /// <param name="builder">Component builder for additional interaction components.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task<IUserMessage> SendConfirmFollowupAsync(this IDiscordInteraction interaction,
        string message, ComponentBuilder builder)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(),
            components: builder.Build());

    /// <summary>
    /// Sends an ephemeral follow-up confirmation message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the follow-up.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task<IUserMessage> SendEphemeralFollowupConfirmAsync(this IDiscordInteraction interaction,
        string message)
        => interaction
            .FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(), ephemeral: true);

    /// <summary>
    /// Sends a follow-up error message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the error.</param>
    /// /// <param name="config">Bot configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task<IUserMessage> SendErrorFollowupAsync(this IDiscordInteraction interaction, string message,
        BotConfig config)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            components: config.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                    .WithButton(label: "Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                    .Build()
                : null);

    /// <summary>
    /// Sends an ephemeral follow-up error message asynchronously.
    /// </summary>
    /// <param name="interaction">Discord interaction context.</param>
    /// <param name="message">Message to include in the error.</param>
    /// <param name="config">Bot configuration.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static Task<IUserMessage> SendEphemeralFollowupErrorAsync(this IDiscordInteraction interaction,
        string message, BotConfig config)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            ephemeral: true, components: config.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                    .WithButton(label: "Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                    .Build()
                : null);

    /// <summary>
    /// Checks if the first attachment in the collection is a valid music URL.
    /// </summary>
    /// <param name="attachments">Collection of attachments.</param>
    /// <returns>True if the first attachment is a valid music URL, otherwise false.</returns>
    public static bool IsValidAttachment(this IReadOnlyCollection<IAttachment> attachments)
    {
        var first = attachments.FirstOrDefault();
        return first != null && first.Url.CheckIfMusicUrl();
    }

    /// <summary>
    /// Retrieves the IDs of all guilds the Discord socket client is connected to.
    /// </summary>
    /// <param name="client">Discord socket client.</param>
    /// <returns>List of guild IDs.</returns>
    public static List<ulong> GetGuildIds(this DiscordShardedClient client) => client.Guilds.Select(x => x.Id).ToList();

    /// <summary>
    /// Converts a TimeSpan to a pretty formatted string.
    /// </summary>
    /// <param name="span">TimeSpan to convert.</param>
    /// <returns>Formatted duration string.</returns>
    public static string ToPrettyStringHm(this TimeSpan span)
    {
        return span < TimeSpan.FromMinutes(2) ? $"{span:mm}m {span:ss}s" : $"{(int)span.TotalHours:D2}h {span:mm}m";
    }

    /// <summary>
    /// Tries to retrieve a configuration from the list of guild configurations.
    /// </summary>
    /// <param name="configList">List of guild configurations.</param>
    /// <param name="id">Guild ID.</param>
    /// <param name="config">Retrieved guild configuration if found, otherwise null.</param>
    /// <returns>True if the configuration was found, otherwise false.</returns>
    public static bool TryGetConfig(this List<GuildConfig> configList, ulong id, out GuildConfig config)
    {
        var tocheck = configList.Find(x => x.GuildId == id);
        if (tocheck == null)
        {
            config = null;
            return false;
        }

        config = tocheck;
        return true;
    }

    /// <summary>
    /// Adds a range of items to the list.
    /// </summary>
    /// <typeparam name="T">Type of items.</typeparam>
    /// <param name="list">List to add items to.</param>
    /// <param name="items">Items to add.</param>
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (var i in items) list.Add(i);
    }

    /// <summary>
    /// Removes a range of items from the list.
    /// </summary>
    /// <typeparam name="T">Type of items.</typeparam>
    /// <param name="list">List to remove items from.</param>
    /// <param name="items">Items to remove.</param>
    public static void RemoveRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (var i in items) list.Remove(i);
    }


    /// <summary>
    /// Adds a collection of type readers to the command service.
    /// </summary>
    /// <typeparam name="TResult">Result type of the type readers.</typeparam>
    /// <param name="commands">Command service to add type readers to.</param>
    /// <param name="readers">Type readers to add.</param>
    public static void AddTypeReaders<TResult>(this CommandService commands, params TypeReader[] readers)
        => commands.AddTypeReader<TResult>(new TypeReaderCollection(readers));

    /// <summary>
    /// Tries to extract the path from a URL string.
    /// </summary>
    /// <param name="input">Input URL string.</param>
    /// <param name="path">Extracted path from the URL.</param>
    /// <returns>True if the path extraction is successful, otherwise false.</returns>
    public static bool TryGetUrlPath(this string input, out string path)
    {
        var match = UrlRegex.Match(input);
        if (match.Success)
        {
            path = match.Groups["path"].Value;
            return true;
        }

        path = string.Empty;
        return false;
    }

    /// <summary>
    /// Converts a string representation of an emoji to an IEmote.
    /// </summary>
    /// <param name="emojiStr">String representation of the emoji.</param>
    /// <returns>The corresponding IEmote instance.</returns>
    public static IEmote? ToIEmote(this string emojiStr) =>
        Emote.TryParse(emojiStr, out var maybeEmote)
            ? maybeEmote
            : new Emoji(emojiStr);

    /// <summary>
    /// Tries to convert a string representation of an emoji to an IEmote.
    /// </summary>
    /// <param name="emojiStr">String representation of the emoji.</param>
    /// <param name="value">Resulting IEmote instance.</param>
    /// <returns>True if conversion is successful, otherwise false.</returns>
    public static bool TryToIEmote(this string emojiStr, out IEmote value)
    {
        value = Emote.TryParse(emojiStr, out var emoteValue)
            ? emoteValue
            : Emoji.TryParse(emojiStr, out var emojiValue)
                ? emojiValue
                : null;
        return value is not null;
    }

    /// <summary>
    /// Retrieves the first 10 characters of the bot token.
    /// </summary>
    /// <param name="bc">Bot credentials.</param>
    /// <returns>First 10 characters of the bot token.</returns>
    public static string RedisKey(this IBotCredentials bc) => bc.Token[..10];

    /// <summary>
    /// Asynchronously replaces matches in a string with the result of a delegate function.
    /// </summary>
    /// <param name="regex">Regular expression pattern.</param>
    /// <param name="input">Input string.</param>
    /// <param name="replacementFn">Function to generate replacement strings.</param>
    /// <returns>Task representing the asynchronous operation with the replaced string.</returns>
    public static async Task<string?> ReplaceAsync(this Regex regex, string? input,
        Func<Match, Task<string>> replacementFn)
    {
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in regex.Matches(input))
        {
            sb.Append(input, lastIndex, match.Index - lastIndex)
                .Append(await replacementFn(match).ConfigureAwait(false));

            lastIndex = match.Index + match.Length;
        }

        sb.Append(input, lastIndex, input.Length - lastIndex);
        return sb.ToString();
    }

    /// <summary>
    /// Throws an ArgumentNullException if the provided object is null.
    /// </summary>
    /// <typeparam name="T">Type of the object.</typeparam>
    /// <param name="o">Object to check.</param>
    /// <param name="name">Name of the object (for exception message).</param>
    public static void ThrowIfNull<T>(this T o, string name) where T : class
    {
        if (o == null)
            throw new ArgumentNullException(nameof(o));
    }

    /// <summary>
    /// Checks if the message author is the bot itself.
    /// </summary>
    /// <param name="msg">Message to check.</param>
    /// <param name="client">Discord client.</param>
    /// <returns>True if the author is the bot, otherwise false.</returns>
    public static bool IsAuthor(this IMessage msg, IDiscordClient client) => msg.Author?.Id == client.CurrentUser.Id;

    /// <summary>
    /// Retrieves the real summary of a command.
    /// </summary>
    /// <param name="cmd">Command information.</param>
    /// <param name="strings">Bot strings provider.</param>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="prefix">Command prefix.</param>
    /// <returns>Real summary of the command.</returns>
    public static string RealSummary(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string? prefix) =>
        string.Format(strings.GetCommandStrings(cmd.Name, guildId).Desc, prefix);

    /// <summary>
    /// Retrieves the real remarks array of a command.
    /// </summary>
    /// <param name="cmd">Command information.</param>
    /// <param name="strings">Bot strings provider.</param>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="prefix">Command prefix.</param>
    /// <returns>Real remarks array of the command.</returns>
    public static string[] RealRemarksArr(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string? prefix) =>
        Array.ConvertAll(strings.GetCommandStrings(cmd.MethodName(), guildId).Args,
            arg => GetFullUsage(cmd.Name, arg, prefix));

    /// <summary>
    /// Retrieves the method name of a command.
    /// </summary>
    /// <param name="cmd">Command information.</param>
    /// <returns>Method name of the command.</returns>
    public static string MethodName(this CommandInfo cmd) =>
        ((Cmd)cmd.Attributes.FirstOrDefault(x => x is Cmd))?.MethodName
        ?? cmd.Name;

    /// <summary>
    /// Generates the full usage of a command with the provided arguments and prefix.
    /// </summary>
    /// <param name="commandName">Name of the command.</param>
    /// <param name="args">Arguments of the command.</param>
    /// <param name="prefix">Command prefix.</param>
    /// <returns>Full usage of the command.</returns>
    public static string GetFullUsage(string commandName, string args, string? prefix) =>
        $"{prefix}{commandName} {(StringExtensions.TryFormat(args, [prefix], out var output) ? output : args)}";

    /// <summary>
    /// Adds a paginated footer to an embed.
    /// </summary>
    /// <param name="embed">Embed to add the footer to.</param>
    /// <param name="curPage">Current page number.</param>
    /// <param name="lastPage">Last page number.</param>
    /// <returns>Embed builder with the added footer.</returns>
    public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
    {
        if (lastPage != null)
            return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
        return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
    }

    /// <summary>
    /// Sets the color of an embed to OK color.
    /// </summary>
    /// <param name="eb">Embed builder to set the color for.</param>
    /// <returns>Embed builder with the color set to OK color.</returns>
    public static EmbedBuilder WithOkColor(this EmbedBuilder eb) => eb.WithColor(Mewdeko.OkColor);


    /// <summary>
    /// Sets the color of an embed to the error color.
    /// </summary>
    /// <param name="eb">Embed builder to set the color for.</param>
    /// <returns>Embed builder with the color set to the error color.</returns>
    public static EmbedBuilder WithErrorColor(this EmbedBuilder eb) => eb.WithColor(Mewdeko.ErrorColor);

    /// <summary>
    /// Sets the color of a page builder to the OK color.
    /// </summary>
    /// <param name="eb">Page builder to set the color for.</param>
    /// <returns>Page builder with the color set to the OK color.</returns>
    public static PageBuilder WithOkColor(this PageBuilder eb) => eb.WithColor(Mewdeko.OkColor);

    /// <summary>
    /// Sets the color of a page builder to the error color.
    /// </summary>
    /// <param name="eb">Page builder to set the color for.</param>
    /// <returns>Page builder with the color set to the error color.</returns>
    public static PageBuilder WithErrorColor(this PageBuilder eb) => eb.WithColor(Mewdeko.ErrorColor);

    /// <summary>
    /// Adds fake headers to the HttpClient.
    /// </summary>
    /// <param name="http">HttpClient to add headers to.</param>
    /// <returns>HttpClient with fake headers added.</returns>
    public static HttpClient AddFakeHeaders(this HttpClient http)
    {
        http.DefaultRequestHeaders.AddFakeHeaders();
        return http;
    }

    /// <summary>
    /// Adds fake headers to the HttpHeaders dictionary.
    /// </summary>
    /// <param name="dict">HttpHeaders dictionary to add headers to.</param>
    public static void AddFakeHeaders(this HttpHeaders dict)
    {
        dict.Clear();
        dict.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        dict.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.202 Safari/535.1");
    }

    /// <summary>
    /// Deletes a message after a specified number of seconds.
    /// </summary>
    /// <param name="msg">Message to delete.</param>
    /// <param name="seconds">Number of seconds to wait before deleting.</param>
    public static void DeleteAfter(this IUserMessage? msg, int seconds)
    {
        if (msg is null) return;

        Task.Run(async () =>
        {
            await Task.Delay(seconds * 1000).ConfigureAwait(false);
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });
    }

    /// <summary>
    /// Deletes a message after a specified number of seconds.
    /// </summary>
    /// <param name="msg">Message to delete.</param>
    /// <param name="seconds">Number of seconds to wait before deleting.</param>
    public static void DeleteAfter(this IMessage? msg, int seconds)
    {
        if (msg is null) return;

        Task.Run(async () =>
        {
            await Task.Delay(seconds * 1000).ConfigureAwait(false);
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });
    }

    /// <summary>
    /// Gets the top-level module of the given module.
    /// </summary>
    /// <param name="module">Module to get the top-level module for.</param>
    /// <returns>Top-level module of the given module.</returns>
    public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
    {
        while (module.Parent != null) module = module.Parent;
        return module;
    }

    /// <summary>
    /// Gets the members with the specified role asynchronously.
    /// </summary>
    /// <param name="role">Role to get the members for.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static async Task<IEnumerable<IGuildUser>> GetMembersAsync(this IRole role) =>
        (await role.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false)).Where(u =>
            u.RoleIds.Contains(role.Id));


    /// <summary>
    /// Converts an SKImage to a MemoryStream.
    /// </summary>
    /// <param name="img">SKImage to convert.</param>
    /// <param name="format">Encoded image format (default is PNG).</param>
    /// <returns>MemoryStream containing the encoded image data.</returns>
    public static MemoryStream ToStream(this SKImage img, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        var data = img.Encode(format, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Converts a collection of bytes to a Stream.
    /// </summary>
    /// <param name="bytes">Collection of bytes to convert.</param>
    /// <param name="canWrite">Boolean indicating if the stream can be written to (default is false).</param>
    /// <returns>Stream containing the bytes.</returns>
    public static Stream ToStream(this IEnumerable<byte> bytes, bool canWrite = false)
    {
        var ms = new MemoryStream(bytes as byte[] ?? bytes.ToArray(), canWrite);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Gets the roles associated with the specified user.
    /// </summary>
    /// <param name="user">User to get the roles for.</param>
    /// <returns>Enumerable collection of roles associated with the user.</returns>
    public static IEnumerable<IRole> GetRoles(this IGuildUser user) =>
        user.RoleIds.Select(r => user.Guild.GetRole(r)).Where(r => r != null);

    /// <summary>
    /// Checks if the HttpResponseMessage contains an image.
    /// </summary>
    /// <param name="msg">HttpResponseMessage to check.</param>
    /// <returns>True if the response contains an image; otherwise, false.</returns>
    public static bool IsImage(this HttpResponseMessage msg) => msg.IsImage(out _);

    /// <summary>
    /// Checks if the HttpResponseMessage contains an image.
    /// </summary>
    /// <param name="msg">HttpResponseMessage to check.</param>
    /// <param name="mimeType">String reference to store the MIME type of the image.</param>
    /// <returns>True if the response contains an image; otherwise, false.</returns>
    public static bool IsImage(this HttpResponseMessage msg, out string? mimeType)
    {
        if (msg.Content.Headers.ContentType != null) _ = msg.Content.Headers.ContentType.MediaType;
        mimeType = msg.Content.Headers.ContentType.MediaType;
        return mimeType is "image/png" or "image/jpeg" or "image/gif";
    }

    /// <summary>
    /// Gets the size of the image contained in the HttpResponseMessage.
    /// </summary>
    /// <param name="msg">HttpResponseMessage containing the image.</param>
    /// <returns>The size of the image in bytes.</returns>
    public static long? GetImageSize(this HttpResponseMessage msg)
    {
        if (msg.Content.Headers.ContentLength == null) return null;
        return msg.Content.Headers.ContentLength / 1.Mb();
    }

    /// <summary>
    /// Converts a byte array to an SKImage.
    /// </summary>
    /// <param name="imageData">Byte array containing the image data.</param>
    /// <returns>SKImage created from the byte array.</returns>
    public static SKImage ToSkImage(this byte[] imageData)
    {
        return SKImage.FromEncodedData(imageData);
    }

    /// <summary>
    /// Gets the command names associated with the IApplicationCommand.
    /// </summary>
    /// <param name="command">IApplicationCommand to get the command names for.</param>
    /// <returns>An array of command names.</returns>
    public static string[] GetCtNames(this IApplicationCommand command)
    {
        var baseName = command.Name;
        var sgs = command.Options.Where(x =>
            x.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup);

        if (!sgs.Any())
            return
            [
                baseName
            ];

        var ctNames = new List<string>();
        foreach (var sg in sgs)
            if (sg.Type == ApplicationCommandOptionType.SubCommand)
                ctNames.Add(baseName + " " + sg.Name);
            else
                ctNames.AddRange(sg.Options.Select(x => baseName + " " + sg.Name + " " + x.Name));

        return ctNames.ToArray();
    }

    /// <summary>
    /// Gets the real name of the interaction.
    /// </summary>
    /// <param name="interaction">SocketInteraction instance.</param>
    /// <returns>The real name of the interaction.</returns>
    public static string GetRealName(this SocketInteraction interaction)
    {
        switch (interaction)
        {
            case SocketUserCommand uCmd:
                return uCmd.Data.Name;
            case SocketMessageCommand mCmd:
                return mCmd.Data.Name;
            default:
            {
                if (interaction is not SocketSlashCommand sCmd)
                    throw new ArgumentException("interaction is not a valid type");
                return (sCmd.Data.Name
                        + " "
                        + ((sCmd.Data.Options?.FirstOrDefault()?.Type is ApplicationCommandOptionType.SubCommand
                               or ApplicationCommandOptionType.SubCommandGroup
                               ? sCmd.Data.Options?.First().Name
                               : "")
                           ?? "")
                        + " "
                        + (sCmd.Data.Options?.FirstOrDefault()?.Options?.FirstOrDefault()?.Type
                           == ApplicationCommandOptionType.SubCommand
                            ? sCmd.Data.Options?.FirstOrDefault()?.Options?.FirstOrDefault()?.Name
                            : "")
                        ?? "").Trim();
            }
        }
    }

    /// <summary>
    /// Generates a regular expression for URL validation.
    /// </summary>
    /// <returns>A compiled regular expression for URL validation.</returns>
    [GeneratedRegex("^(https?|ftp)://(?<path>[^\\s/$.?#].[^\\s]*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}