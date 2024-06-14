using System.Text.RegularExpressions;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using NekosBestApiNet;

namespace Mewdeko.Common.Replacements;

/// <summary>
/// Class that builds replacements for placeholders in text. One of the most useful classes in the bot.
/// </summary>
public partial class ReplacementBuilder
{
    /// <summary>
    /// Regular expression for matching RNG placeholders.
    /// </summary>
    private static readonly Regex RngRegex =
        MyRegex();

    /// <summary>
    /// The Discord client.
    /// </summary>
    private readonly DiscordShardedClient client;

    /// <summary>
    /// The NekosBest API client.
    /// </summary>
    private readonly NekosBestApi nekosBestApi;

    /// <summary>
    /// Dictionary of regular expressions and their replacement functions.
    /// </summary>
    private readonly ConcurrentDictionary<Regex, Func<Match, string>> regex = new();


    /// <summary>
    /// Dictionary of placeholder keys and their replacement functions.
    /// </summary>
    private readonly ConcurrentDictionary<string, Func<string?>> reps = new();

    /// <summary>
    /// Initializes a new instance of the ReplacementBuilder class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    public ReplacementBuilder(DiscordShardedClient? client = null)
    {
        nekosBestApi = new NekosBestApi();
        this.client = client;
        WithRngRegex();
    }

    /// <summary>
    /// Sets up default replacements for a user, channel, guild, and client.
    /// </summary>
    /// <param name="usr">The user.</param>
    /// <param name="ch">The channel.</param>
    /// <param name="g">The guild.</param>
    /// <param name="socketClient">The client.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithDefault(IUser usr, IMessageChannel ch, SocketGuild g,
        DiscordShardedClient socketClient) =>
        WithUser(usr)
            .WithChannel(ch)
            .WithServer(socketClient, g)
            .WithClient(socketClient)
            .WithGifs();

    /// <summary>
    /// Sets up default replacements for a command context.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithDefault(ICommandContext ctx) => WithDefault(ctx.User, ctx.Channel,
        ctx.Guild as SocketGuild, (DiscordShardedClient)ctx.Client);

    /// <summary>
    /// Sets up default replacements for an interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithDefault(IInteractionContext ctx) => WithDefault(ctx.User, ctx.Channel,
        ctx.Guild as SocketGuild, (DiscordShardedClient)ctx.Client);

    /// <summary>
    /// Sets up replacements for a client.
    /// </summary>
    /// <param name="socketClient">The client.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithClient(DiscordShardedClient socketClient)
    {

        /*NEW*/
        reps.TryAdd("%bot.status%", () => socketClient.Status.ToString());
        reps.TryAdd("%bot.latency%", () => socketClient.Latency.ToString());
        reps.TryAdd("%bot.name%", () => socketClient.CurrentUser.Username);
        reps.TryAdd("%bot.fullname%", () => socketClient.CurrentUser.ToString());
        reps.TryAdd("%bot.time%",
            () => DateTime.Now.ToString($"HH:mm {TimeZoneInfo.Local.StandardName.GetInitials()}"));
        reps.TryAdd("%bot.discrim%", () => socketClient.CurrentUser.Discriminator);
        reps.TryAdd("%bot.id%", () => socketClient.CurrentUser.Id.ToString());
        reps.TryAdd("%bot.avatar%", () => socketClient.CurrentUser.RealAvatarUrl().ToString());

        WithStats(socketClient);
        return this;
    }

    /// <summary>
    /// Sets up replacements for a server.
    /// </summary>
    /// <param name="socketClient">The client.</param>
    /// <param name="g">The server.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithServer(DiscordShardedClient socketClient, SocketGuild? g)
    {
        /*OBSOLETE*/
        reps.TryAdd("%sid%", () => g == null ? "DM" : g.Id.ToString());
        reps.TryAdd("%server%", () => g == null ? "DM" : g.Name);
        reps.TryAdd("%members%", () => g is { } sg ? sg.MemberCount.ToString() : "?");
        reps.TryAdd("%server_time%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to).ToString("HH:mm ") + to.StandardName.GetInitials();
        });
        /*NEW*/
        reps.TryAdd("%time.month%", () => DateTime.UtcNow.ToString("MMMM"));
        reps.TryAdd("%time.day%", () => DateTime.UtcNow.ToString("dddd"));
        reps.TryAdd("%time.year%", () => DateTime.UtcNow.ToString("yyyy"));
        reps.TryAdd("%server.icon%", () => g == null ? "DM" : $"{g.IconUrl}?size=2048");
        reps.TryAdd("%server.id%", () => g == null ? "DM" : g.Id.ToString());
        reps.TryAdd("%server.name%", () => g == null ? "DM" : g.Name.EscapeWeirdStuff());
        reps.TryAdd("%server.banner%", () => g == null ? "DM" : g.BannerUrl);
        reps.TryAdd("%server.boostlevel%", () =>
        {
            var e = g.PremiumTier.ToString();
            return e.StartsWith("Tier") ? e.Replace("Tier", "") : "0";
        });
        reps.TryAdd("%server.boostcount%", () => g.PremiumSubscriptionCount.ToString());
        reps.TryAdd("%server.members%", () => g is { } sg ? sg.Users.Count.ToString() : "?");
        reps.TryAdd("%server.members.online%",
            () => g is { } sg ? sg.Users.Count(x => x.Status == UserStatus.Online).ToString() : "?");
        reps.TryAdd("%server.members.offline%",
            () => g is { } sg ? sg.Users.Count(x => x.Status == UserStatus.Offline).ToString() : "?");
        reps.TryAdd("%server.members.dnd%",
            () => g is { } sg ? sg.Users.Count(x => x.Status == UserStatus.DoNotDisturb).ToString() : "?");
        reps.TryAdd("%server.members.idle%",
            () => g is { } sg ? sg.Users.Count(x => x.Status == UserStatus.Idle).ToString() : "?");
        reps.TryAdd("%server.timestamp.longdatetime%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimestampTag.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to), TimestampTagStyles.LongDateTime).ToString();
        });
        reps.TryAdd("%server.timestamp.longtime%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimestampTag.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to), TimestampTagStyles.LongTime).ToString();
        });
        reps.TryAdd("%server.timestamp.longdate%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimestampTag.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to), TimestampTagStyles.LongDate).ToString();
        });
        reps.TryAdd("%server.timestamp.shortdatetime%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimestampTag.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to)).ToString();
        });
        reps.TryAdd("%server.time%", () =>
        {
            var to = TimeZoneInfo.Local;
            if (g != null)
                if (GuildTimezoneService.AllServices.TryGetValue(socketClient.CurrentUser.Id, out var tz))
                    to = tz.GetTimeZoneOrDefault(g.Id) ?? TimeZoneInfo.Local;

            return TimeZoneInfo.ConvertTime(DateTime.UtcNow,
                TimeZoneInfo.Utc,
                to).ToString("HH:mm ") + to.StandardName.GetInitials();
        });
        return this;
    }

    /// <summary>
    /// Sets up replacements for GIFs.
    /// </summary>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithGifs()
    {
        reps.TryAdd("%bakagif%", () => nekosBestApi.ActionsApi.Baka().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%bitegif%", () => nekosBestApi.ActionsApi.Bite().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%blushgif%", () => nekosBestApi.ActionsApi.Blush().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%boredgif%", () => nekosBestApi.ActionsApi.Bored().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%crygif%", () => nekosBestApi.ActionsApi.Cry().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%cuddlegif%", () => nekosBestApi.ActionsApi.Cuddle().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%dancegif%", () => nekosBestApi.ActionsApi.Dance().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%facepalmgif%",
            () => nekosBestApi.ActionsApi.Facepalm().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%feedgif", () => nekosBestApi.ActionsApi.Feed().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%handholdgif%",
            () => nekosBestApi.ActionsApi.Handhold().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%happygif%", () => nekosBestApi.ActionsApi.Happy().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%highfivegif%",
            () => nekosBestApi.ActionsApi.Highfive().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%huggif%", () => nekosBestApi.ActionsApi.Hug().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%kickgif%", () => nekosBestApi.ActionsApi.Kick().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%kissgif%", () => nekosBestApi.ActionsApi.Kiss().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%laughgif%", () => nekosBestApi.ActionsApi.Laugh().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%patgif%", () => nekosBestApi.ActionsApi.Pat().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%pokegif%", () => nekosBestApi.ActionsApi.Poke().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%poutgif%", () => nekosBestApi.ActionsApi.Pout().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%punchgif%", () => nekosBestApi.ActionsApi.Punch().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%shootgif%", () => nekosBestApi.ActionsApi.Shoot().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%shruggif%", () => nekosBestApi.ActionsApi.Shrug().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%slapgif%", () => nekosBestApi.ActionsApi.Slap().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%sleepgif%", () => nekosBestApi.ActionsApi.Sleep().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%smilegif%", () => nekosBestApi.ActionsApi.Smile().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%smuggif%", () => nekosBestApi.ActionsApi.Smug().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%staregif%", () => nekosBestApi.ActionsApi.Stare().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%thinkgif%", () => nekosBestApi.ActionsApi.Think().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%thumbsupgif%",
            () => nekosBestApi.ActionsApi.Thumbsup().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%ticklegif%", () => nekosBestApi.ActionsApi.Tickle().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%wavegif%", () => nekosBestApi.ActionsApi.Wave().GetAwaiter().GetResult().Results.First().Url);
        reps.TryAdd("%winkgif%", () => nekosBestApi.ActionsApi.Wink().GetAwaiter().GetResult().Results.First().Url);
        return this;
    }

    /// <summary>
    /// Sets up replacements for a channel.
    /// </summary>
    /// <param name="ch">The channel.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithChannel(IMessageChannel? ch)
    {
        /*OBSOLETE*/
        reps.TryAdd("%channel%", () => (ch as ITextChannel)?.Mention ?? $"#{ch.Name}");
        reps.TryAdd("%chname%", () => ch.Name);
        reps.TryAdd("%cid%", () => ch?.Id.ToString());
        /*NEW*/
        reps.TryAdd("%channel.mention%", () => (ch as ITextChannel)?.Mention ?? $"#{ch.Name}");
        reps.TryAdd("%channel.name%", () => ch.Name.EscapeWeirdStuff());
        reps.TryAdd("%channel.id%", () => ch.Id.ToString());
        reps.TryAdd("%channel.created%", () => ch.CreatedAt.ToString("HH:mm dd.MM.yyyy"));
        reps.TryAdd("%channel.nsfw%", () => (ch as ITextChannel)?.IsNsfw.ToString() ?? "-");
        reps.TryAdd("%channel.topic%", () => (ch as ITextChannel)?.Topic.EscapeWeirdStuff() ?? "-");
        return this;
    }

    /// <summary>
    /// Sets up replacements for a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithUser(IUser user)
    {
        WithManyUsers(new[]
        {
            user
        });
        return this;
    }

    /// <summary>
    /// Sets up replacements for multiple users.
    /// </summary>
    /// <param name="users">The users.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithManyUsers(IEnumerable<IUser> users)
    {
        /*OBSOLETE*/
        reps.TryAdd("%user%", () => string.Join(" ", users.Select(user => user.Mention)));
        reps.TryAdd("%userfull%", () => string.Join(" ", users.Select(user => user.ToString().EscapeWeirdStuff())));
        reps.TryAdd("%username%", () => string.Join(" ", users.Select(user => user.Username.EscapeWeirdStuff())));
        reps.TryAdd("%userdiscrim%", () => string.Join(" ", users.Select(user => user.Discriminator)));
        reps.TryAdd("%useravatar%",
            () => string.Join(" ", users.Select(user => user.RealAvatarUrl().ToString())));
        reps.TryAdd("%id%", () => string.Join(" ", users.Select(user => user.Id.ToString())));
        reps.TryAdd("%uid%", () => string.Join(" ", users.Select(user => user.Id.ToString())));
        /*NEW*/
        reps.TryAdd("%user.mention%", () => string.Join(" ", users.Select(user => user.Mention)));
        reps.TryAdd("%user.fullname%",
            () => string.Join(" ", users.Select(user => user.ToString().EscapeWeirdStuff())));
        reps.TryAdd("%user.name%", () => string.Join(" ", users.Select(user => user.Username.EscapeWeirdStuff())));
        reps.TryAdd("%user.banner%",
            () => string.Join(" ",
                users.Select(async user =>
                    (await client.Rest.GetUserAsync(user.Id).ConfigureAwait(false)).GetBannerUrl(size: 2048))));
        reps.TryAdd("%user.discrim%", () => string.Join(" ", users.Select(user => user.Discriminator)));
        reps.TryAdd("%user.avatar%",
            () => string.Join(" ", users.Select(user => user.RealAvatarUrl().ToString())));
        reps.TryAdd("%user.id%", () => string.Join(" ", users.Select(user => user.Id.ToString())));
        reps.TryAdd("%user.created_time%",
            () => string.Join(" ", users.Select(user => user.CreatedAt.ToString("HH:mm"))));
        reps.TryAdd("%user.created_date%",
            () => string.Join(" ", users.Select(user => user.CreatedAt.ToString("dd.MM.yyyy"))));
        reps.TryAdd("%user.joined_time%",
            () => string.Join(" ", users.Select(user => (user as IGuildUser)?.JoinedAt?.ToString("HH:mm") ?? "-")));
        reps.TryAdd("%user.joined_date%",
            () => string.Join(" ",
                users.Select(user => (user as IGuildUser)?.JoinedAt?.ToString("dd.MM.yyyy") ?? "-")));
        return this;
    }

    /// <summary>
    /// Sets up replacements for RNG placeholders.
    /// </summary>
    /// <returns>The ReplacementBuilder instance.</returns>
    private void WithStats(DiscordShardedClient c)
    {
        /*OBSOLETE*/
        reps.TryAdd("%servers%", () => c.Guilds.Count.ToString());
#if !GLOBAL_Mewdeko
        reps.TryAdd("%users%", () => c.Guilds.Sum(s => s.Users.Count).ToString());
#endif

        /*NEW*/
        reps.TryAdd("%shard.servercount%", () => c.Guilds.Count.ToString());
#if !GLOBAL_Mewdeko
        reps.TryAdd("%shard.usercount%", () => c.Guilds.Sum(s => s.Users.Count).ToString());
#endif
    }

    /// <summary>
    /// Sets up replacements for RNG placeholders.
    /// </summary>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithRngRegex()
    {
        var rng = new MewdekoRandom();
        regex.TryAdd(RngRegex, match =>
        {
            if (!int.TryParse(match.Groups["from"].ToString(), out var from))
                from = 0;
            if (!int.TryParse(match.Groups["to"].ToString(), out var to))
                to = 0;

            if (from == 0 && to == 0)
                return rng.Next(0, 11).ToString();

            return from >= to ? string.Empty : rng.Next(from, to + 1).ToString();
        });
        return this;
    }

    /// <summary>
    /// Overrides a replacement.
    /// </summary>
    /// <param name="key">The key of the replacement to override.</param>
    /// <param name="output">The new replacement function.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithOverride(string key, Func<string?> output)
    {
        reps.AddOrUpdate(key, output, delegate { return output; });
        return this;
    }

    /// <summary>
    /// Builds a Replacer instance.
    /// </summary>
    /// <returns>The built Replacer instance.</returns>
    public Replacer Build() =>
        new(reps.Select(x => (x.Key, x.Value)).ToArray(),
            regex.Select(x => (x.Key, x.Value)).ToArray());

    /// <summary>
    /// Sets up replacements using placeholder providers.
    /// </summary>
    /// <param name="phProviders">The placeholder providers.</param>
    /// <returns>The ReplacementBuilder instance.</returns>
    public ReplacementBuilder WithProviders(IEnumerable<IPlaceholderProvider> phProviders)
    {
        foreach (var provider in phProviders)
        foreach (var ovr in provider.GetPlaceholders())
            reps.TryAdd(ovr.Name, ovr.Func);

        return this;
    }

    /// <summary>
    /// Generates a regular expression for matching RNG placeholders.
    /// </summary>
    /// <returns>A compiled regular expression.</returns>
    [GeneratedRegex("%rng(?:(?<from>(?:-)?\\d+)-(?<to>(?:-)?\\d+))?%", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}