using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     A site whose links are rewritten to an embed fixing service.
/// </summary>
/// <param name="Name">The key used in commands and stored settings.</param>
/// <param name="DisplayName">The name shown to users.</param>
/// <param name="DefaultHost">The embed fixing host used when a server has not set its own.</param>
/// <param name="Hosts">The hosts of the site's links.</param>
/// <param name="Path">Matches the paths of the links that get rewritten, such as posts rather than profiles.</param>
/// <param name="KeptSubdomains">Subdomains carried over to the fixing host, for short link domains like vm.tiktok.com.</param>
public sealed record LinkFixPlatform(
    string Name,
    string DisplayName,
    string DefaultHost,
    Regex Hosts,
    Regex Path,
    string[] KeptSubdomains);

/// <summary>
///     The supported sites and the rewriting of their links.
/// </summary>
public static partial class LinkFixPlatforms
{
    /// <summary>
    ///     The most links fixed from one message.
    /// </summary>
    public const int MaxLinksPerMessage = 5;

    /// <summary>
    ///     The supported sites.
    /// </summary>
    public static readonly IReadOnlyList<LinkFixPlatform> All =
    [
        new("twitter", "Twitter/X", "fxtwitter.com", TwitterHosts(), TwitterPath(), []),
        new("instagram", "Instagram", "vxinstagram.com", InstagramHosts(), InstagramPath(), []),
        new("tiktok", "TikTok", "tnktok.com", TikTokHosts(), TikTokPath(), ["vm", "vt"]),
        new("reddit", "Reddit", "vxreddit.com", RedditHosts(), RedditPath(), []),
        new("pinterest", "Pinterest", "pinterestez.com", PinterestHosts(), PinterestPath(), []),
        new("bluesky", "Bluesky", "fxbsky.app", BlueskyHosts(), BlueskyPath(), []),
        new("pixiv", "Pixiv", "phixiv.net", PixivHosts(), PixivPath(), [])
    ];

    /// <summary>
    ///     Gets a supported site by its key, ignoring case.
    /// </summary>
    public static LinkFixPlatform? Find(string name) =>
        All.FirstOrDefault(platform => platform.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Whether a value is a plain hostname that can be used as a fixing host.
    /// </summary>
    public static bool IsValidHost(string host) => HostName().IsMatch(host);

    /// <summary>
    ///     Gets the fixed versions of the supported links in a message. Links inside code, links wrapped in angle
    ///     brackets and links to disabled sites are skipped, and links inside spoilers stay inside spoilers.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="hostFor">Gets the fixing host for a site, or null when the site is turned off.</param>
    public static List<string> FixLinks(string content, Func<LinkFixPlatform, string?> hostFor)
    {
        var fixedLinks = new List<string>();
        if (string.IsNullOrEmpty(content) || !content.Contains("http", StringComparison.OrdinalIgnoreCase))
            return fixedLinks;

        foreach (Match match in Url().Matches(content))
        {
            var index = match.Index;
            if (index > 0 && content[index - 1] == '<')
                continue;
            if (CountBefore(content, index, "`") % 2 == 1)
                continue;

            var link = TrimTrailing(match.Value);
            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
                continue;

            var host = uri.Host.ToLowerInvariant();
            var platform = All.FirstOrDefault(p => p.Hosts.IsMatch(host) && p.Path.IsMatch(uri.AbsolutePath));
            if (platform is null || hostFor(platform) is not { } fixHost)
                continue;

            var subdomain = host.Split('.')[0];
            var target = platform.KeptSubdomains.Contains(subdomain) ? $"{subdomain}.{fixHost}" : fixHost;
            var rewritten = $"https://{target}{uri.PathAndQuery}";
            if (CountBefore(content, index, "||") % 2 == 1)
                rewritten = $"||{rewritten}||";

            if (fixedLinks.Contains(rewritten))
                continue;

            fixedLinks.Add(rewritten);
            if (fixedLinks.Count == MaxLinksPerMessage)
                break;
        }

        return fixedLinks;
    }

    private static int CountBefore(string content, int index, string token)
    {
        var count = 0;
        for (var at = content.IndexOf(token, StringComparison.Ordinal); at >= 0 && at < index;
             at = content.IndexOf(token, at + token.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string TrimTrailing(string link)
    {
        link = link.TrimEnd('.', ',', '!', '?', ':', ';', '\'', '"', '*', '_', '~');
        while (link.EndsWith(')') && link.Count(c => c == ')') > link.Count(c => c == '('))
            link = link[..^1];
        return link;
    }

    [GeneratedRegex(@"https?://[^\s<>|`]+", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.IgnoreCase)]
    private static partial Regex HostName();

    [GeneratedRegex(@"^(www\.|mobile\.)?(twitter|x)\.com$")]
    private static partial Regex TwitterHosts();

    [GeneratedRegex(@"^/[^/]+/status/\d+", RegexOptions.IgnoreCase)]
    private static partial Regex TwitterPath();

    [GeneratedRegex(@"^(www\.)?instagram\.com$")]
    private static partial Regex InstagramHosts();

    [GeneratedRegex(@"^(/[^/]+)?/(p|reel|reels|tv)/[^/]+", RegexOptions.IgnoreCase)]
    private static partial Regex InstagramPath();

    [GeneratedRegex(@"^(www\.|m\.|vm\.|vt\.)?tiktok\.com$")]
    private static partial Regex TikTokHosts();

    [GeneratedRegex(@"^(/@[^/]+/(video|photo)/\d+|/t/[^/]+|/[A-Za-z0-9]+/?$)")]
    private static partial Regex TikTokPath();

    [GeneratedRegex(@"^(www\.|old\.|new\.|m\.)?reddit\.com$")]
    private static partial Regex RedditHosts();

    [GeneratedRegex(@"(/comments/[a-z0-9]+|^/r/[^/]+/s/[^/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RedditPath();

    [GeneratedRegex(@"^([a-z]{2}\.|www\.)?pinterest\.(com|[a-z]{2}|co\.[a-z]{2}|com\.[a-z]{2})$")]
    private static partial Regex PinterestHosts();

    [GeneratedRegex(@"^/pin/\d+", RegexOptions.IgnoreCase)]
    private static partial Regex PinterestPath();

    [GeneratedRegex(@"^bsky\.app$")]
    private static partial Regex BlueskyHosts();

    [GeneratedRegex(@"^/profile/[^/]+/post/[^/]+", RegexOptions.IgnoreCase)]
    private static partial Regex BlueskyPath();

    [GeneratedRegex(@"^(www\.)?pixiv\.net$")]
    private static partial Regex PixivHosts();

    [GeneratedRegex(@"^(/[a-z]{2})?/artworks/\d+", RegexOptions.IgnoreCase)]
    private static partial Regex PixivPath();
}
