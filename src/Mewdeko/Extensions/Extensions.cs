#nullable enable
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Services.strings;
using SkiaSharp;
using ModuleInfo = Discord.Commands.ModuleInfo;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko.Extensions;

public static partial class Extensions
{
    public static readonly Regex UrlRegex = MyRegex();

    public static TOut[] Map<TIn, TOut>(this TIn[] arr, Func<TIn, TOut> f) => Array.ConvertAll(arr, x => f(x));

    // public static bool ParseBoth(this bool _, string value)
    // {
    //     switch (value)
    //     {
    //         case null:
    //             throw new ArgumentNullException(nameof(value));
    //         case "0":
    //         case "1":
    //             return value == "1";
    //     }
    //
    //     if (bool.TryParse(value, out var result))
    //         return result;
    //
    //     throw new FormatException($"The value '{value}' is not a valid boolean representation.");
    // }

    public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, CrEmbed crEmbed,
        bool sanitizeAll = false)
    {
        var plainText = sanitizeAll
            ? crEmbed.PlainText?.SanitizeAllMentions() ?? ""
            : crEmbed.PlainText?.SanitizeMentions() ?? "";

        return channel.SendMessageAsync(plainText, embed: crEmbed.IsEmbedValid ? crEmbed.ToEmbed().Build() : null);
    }

    public static Task SendConfirmAsync(this IDiscordInteraction interaction, string? message)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build());

    public static Task SendEphemeralConfirmAsync(this IDiscordInteraction interaction, string message)
        => interaction
            .RespondAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(), ephemeral: true);

    public static Task SendErrorAsync(this IDiscordInteraction interaction, string? message)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            components: new ComponentBuilder()
                .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                .Build());

    public static Task SendEphemeralErrorAsync(this IDiscordInteraction interaction, string? message)
        => interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            ephemeral: true, components: new ComponentBuilder()
                .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                .Build());

    public static Task<IUserMessage> SendConfirmFollowupAsync(this IDiscordInteraction interaction,
        string message)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build());

    public static Task<IUserMessage> SendConfirmFollowupAsync(this IDiscordInteraction interaction,
        string message, ComponentBuilder builder)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(),
            components: builder.Build());

    public static Task<IUserMessage> SendEphemeralFollowupConfirmAsync(this IDiscordInteraction interaction,
        string message)
        => interaction
            .FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(message).Build(), ephemeral: true);

    public static Task<IUserMessage> SendErrorFollowupAsync(this IDiscordInteraction interaction, string message)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            components: new ComponentBuilder()
                .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                .Build());

    public static Task<IUserMessage> SendEphemeralFollowupErrorAsync(this IDiscordInteraction interaction,
        string message)
        => interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            ephemeral: true, components: new ComponentBuilder()
                .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                .Build());

    public static bool IsValidAttachment(this IReadOnlyCollection<IAttachment> attachments)
    {
        var first = attachments.FirstOrDefault();
        return first != null && first.Url.CheckIfMusicUrl();
    }

    public static List<ulong> GetGuildIds(this DiscordSocketClient client) => client.Guilds.Select(x => x.Id).ToList();

    // ReSharper disable once InvalidXmlDocComment
    /// Generates a string in the format 00:mm:ss if timespan is less than 2m.
    /// </summary>
    /// <param name="span">Timespan to convert to string</param>
    /// <returns>Formatted duration string</returns>
    public static string ToPrettyStringHm(this TimeSpan span)
    {
        return span < TimeSpan.FromMinutes(2) ? $"{span:mm}m {span:ss}s" : $"{(int)span.TotalHours:D2}h {span:mm}m";
    }

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

    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (var i in items) list.Add(i);
    }

    public static void RemoveRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (var i in items) list.Remove(i);
    }

    public static void AddTypeReaders<TResult>(this CommandService commands, params TypeReader[] readers)
        => commands.AddTypeReader<TResult>(new TypeReaderCollection(readers));

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

    public static IEmote? ToIEmote(this string emojiStr) =>
        Emote.TryParse(emojiStr, out var maybeEmote)
            ? maybeEmote
            : new Emoji(emojiStr);

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
    ///     First 10 characters of teh bot token.
    /// </summary>
    public static string RedisKey(this IBotCredentials bc) => bc.Token[..10];

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

    public static void ThrowIfNull<T>(this T o, string name) where T : class
    {
        if (o == null)
            throw new ArgumentNullException(nameof(o));
    }

    public static bool IsAuthor(this IMessage msg, IDiscordClient client) => msg.Author?.Id == client.CurrentUser.Id;

    public static string RealSummary(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string? prefix) =>
        string.Format(strings.GetCommandStrings(cmd.Name, guildId).Desc, prefix);

    public static string[] RealRemarksArr(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string? prefix) =>
        Array.ConvertAll(strings.GetCommandStrings(cmd.MethodName(), guildId).Args,
            arg => GetFullUsage(cmd.Name, arg, prefix));

    public static string[] RealRemarksArr(this SlashCommandInfo cmd, IBotStrings strings, ulong? guildId,
        string? prefix) =>
        Array.ConvertAll(strings.GetCommandStrings(cmd.Name, guildId).Args,
            arg => GetFullUsage(cmd.Name, arg, prefix));

    public static string MethodName(this CommandInfo cmd) =>
        ((Cmd)cmd.Attributes.FirstOrDefault(x => x is Cmd))?.MethodName
        ?? cmd.Name;
    // public static string RealRemarks(this CommandInfo cmd, IBotStrings strings, string prefix)
    //     => string.Join('\n', cmd.RealRemarksArr(strings, prefix));

    public static string GetFullUsage(string commandName, string args, string? prefix) =>
        $"{prefix}{commandName} {(StringExtensions.TryFormat(args, new object[] { prefix }, out var output) ? output : args)}";

    public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
    {
        if (lastPage != null)
            return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
        return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
    }

    public static EmbedBuilder WithOkColor(this EmbedBuilder eb) => eb.WithColor(Mewdeko.OkColor);

    public static EmbedBuilder WithErrorColor(this EmbedBuilder eb) => eb.WithColor(Mewdeko.ErrorColor);

    public static PageBuilder WithOkColor(this PageBuilder eb) => eb.WithColor(Mewdeko.OkColor);

    public static PageBuilder WithErrorColor(this PageBuilder eb) => eb.WithColor(Mewdeko.ErrorColor);

    public static HttpClient AddFakeHeaders(this HttpClient http)
    {
        http.DefaultRequestHeaders.AddFakeHeaders();
        return http;
    }

    public static void AddFakeHeaders(this HttpHeaders dict)
    {
        dict.Clear();
        dict.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        dict.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.202 Safari/535.1");
    }

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

    public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
    {
        while (module.Parent != null) module = module.Parent;
        return module;
    }

    public static async Task<IEnumerable<IGuildUser>> GetMembersAsync(this IRole role) =>
        (await role.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false)).Where(u =>
            u.RoleIds.Contains(role.Id));


    public static MemoryStream ToStream(this SKImage img, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        var data = img.Encode(format, 100);
        var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return stream;
    }


    public static Stream ToStream(this IEnumerable<byte> bytes, bool canWrite = false)
    {
        var ms = new MemoryStream(bytes as byte[] ?? bytes.ToArray(), canWrite);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public static IEnumerable<IRole> GetRoles(this IGuildUser user) =>
        user.RoleIds.Select(r => user.Guild.GetRole(r)).Where(r => r != null);

    public static bool IsImage(this HttpResponseMessage msg) => msg.IsImage(out _);

    public static bool IsImage(this HttpResponseMessage msg, out string? mimeType)
    {
        if (msg.Content.Headers.ContentType != null) _ = msg.Content.Headers.ContentType.MediaType;
        mimeType = msg.Content.Headers.ContentType.MediaType;
        return mimeType is "image/png" or "image/jpeg" or "image/gif";
    }

    public static long? GetImageSize(this HttpResponseMessage msg)
    {
        if (msg.Content.Headers.ContentLength == null) return null;

        return msg.Content.Headers.ContentLength / 1.Mb();
    }

    public static SKImage ToSkImage(this byte[] imageData)
    {
        return SKImage.FromEncodedData(imageData);
    }


    // public static SlashCommandOptionBuilder AddOptions(this SlashCommandOptionBuilder builder, IEnumerable<SlashCommandOptionBuilder> options)
    // {
    //     foreach (var option in options)
    //     {
    //         builder.AddOption(option);
    //     }
    //
    //     return builder;
    // }

    public static string[] GetCtNames(this IApplicationCommand command)
    {
        var baseName = command.Name;
        var sgs = command.Options.Where(x =>
            x.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup);

        if (!sgs.Any())
            return new[]
            {
                baseName
            };

        var ctNames = new List<string>();
        foreach (var sg in sgs)
            if (sg.Type == ApplicationCommandOptionType.SubCommand)
                ctNames.Add(baseName + " " + sg.Name);
            else
                ctNames.AddRange(sg.Options.Select(x => baseName + " " + sg.Name + " " + x.Name));

        return ctNames.ToArray();
    }

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

    [GeneratedRegex("^(https?|ftp)://(?<path>[^\\s/$.?#].[^\\s]*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}