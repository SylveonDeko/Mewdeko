using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Common.Yml;
using Newtonsoft.Json;

namespace Mewdeko.Extensions;

public static class StringExtensions
{
    private static readonly HashSet<char> LettersAndDigits = new(Enumerable.Range(48, 10)
        .Concat(Enumerable.Range(65, 26))
        .Concat(Enumerable.Range(97, 26))
        .Select(x => (char)x));

    private static readonly Regex FilterRegex =
        new(@"discord(?:\.gg|\.io|\.me|\.li|(?:app)?\.com\/invite)\/(\w+)", RegexOptions.Compiled |
                                                                            RegexOptions.IgnoreCase);

    private static readonly Regex CodePointRegex
        = new(@"(\\U(?<code>[a-zA-Z0-9]{8})|\\u(?<code>[a-zA-Z0-9]{4})|\\x(?<code>[a-zA-Z0-9]{2}))",
            RegexOptions.Compiled);

    public static string GenerateSecureString(int length)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var sb = new StringBuilder();
        var rnd = new Random();

        for (var i = 0; i < length; i++)
        {
            var index = rnd.Next(chars.Length);
            sb.Append(chars[index]);
        }

        return sb.ToString();
    }

    public static readonly Regex UserMentionsRegex = new(@"<(?:\@!|\@)(?'id'\d{15,19})>", RegexOptions.Compiled);
    public static bool IsNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str);

    public static string PadBoth(this string str, int length)
    {
        var spaces = length - str.Length;
        var padLeft = (spaces / 2) + str.Length;
        return str.PadLeft(padLeft).PadRight(length);
    }

    public static string UnescapeUnicodeCodePoints(this string input) =>
        CodePointRegex.Replace(input, me =>
        {
            var str = me.Groups["code"].Value;
            return YamlHelper.UnescapeUnicodeCodePoint(str);
        });

    public static bool IsImage(this string input) =>
        input.EndsWith(".png") ||
        input.EndsWith(".gif") ||
        input.EndsWith(".jpg") ||
        input.EndsWith(".jpeg");

    public static bool CheckIfMusicUrl(this string input) =>
        input.EndsWith(".mp4") switch
        {
            false when input.EndsWith(".mp3") => true,
            false when input.EndsWith(".flac") => true,
            false when input.EndsWith(".ogg") => true,
            false when input.EndsWith(".wav") => true,
            false when input.EndsWith(".mov") => true,
            false when input.EndsWith(".mp4") => true,
            false => false,
            _ => true
        };

    public static bool CheckIfNotEmbeddable(this string input)
        => input.EndsWith("gifv") || input.EndsWith("mp4");

    public static T MapJson<T>(this string str) => JsonConvert.DeserializeObject<T>(str);

    public static string StripHtml(this string input) => Regex.Replace(input, "<.*?>", string.Empty);

    public static string ToTitleCase(this string str)
    {
        var tokens = str.Split(new[]
        {
            " "
        }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            tokens[i] = token[..1].ToUpperInvariant() + token[1..];
        }

        return string.Join(" ", tokens)
            .Replace(" Of ", " of ")
            .Replace(" The ", " the ");
    }

    /// <summary>
    ///     Removes trailing S or ES (if specified) on the given string if the num is 1
    /// </summary>
    /// <param name="str"></param>
    /// <param name="num"></param>
    /// <param name="es"></param>
    /// <returns>String with the correct singular/plural form</returns>
    public static string SnPl(this string? str, int? num, bool es = false)
    {
        if (str == null)
            throw new ArgumentNullException(nameof(str));
        if (num == null)
            throw new ArgumentNullException(nameof(num));
        return num == 1 ? str.Remove(str.Length - 1, es ? 2 : 1) : str;
    }

    //http://www.dotnetperls.com/levenshtein
    public static int LevenshteinDistance(this string? s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        // Step 1
        if (n == 0) return m;

        if (m == 0) return n;

        // Step 2
        for (var i = 0; i <= n; d[i, 0] = i++)
        {
        }

        for (var j = 0; j <= m; d[0, j] = j++)
        {
        }

        // Step 3
        for (var i = 1; i <= n; i++)
        {
            //Step 4
            for (var j = 1; j <= m; j++)
            {
                // Step 5
                var cost = t[j - 1] == s[i - 1] ? 0 : 1;

                // Step 6
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        // Step 7
        return d[n, m];
    }

    public static async Task<Stream> ToStream(this string str)
    {
        var ms = new MemoryStream();
        var sw = new StreamWriter(ms);
        await sw.WriteAsync(str).ConfigureAwait(false);
        await sw.FlushAsync().ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

    public static bool IsDiscordInvite(this string str) => FilterRegex.IsMatch(str);

    public static string SanitizeMentions(this string? str, bool sanitizeRoleMentions = false)
    {
        str = str.Replace("@everyone", "@everyοne", StringComparison.InvariantCultureIgnoreCase)
            .Replace("@here", "@һere", StringComparison.InvariantCultureIgnoreCase);
        if (sanitizeRoleMentions)
            str = str.SanitizeRoleMentions();

        return str;
    }

    public static string SanitizeRoleMentions(this string? str) => str.Replace("<@&", "<ම&", StringComparison.InvariantCultureIgnoreCase);

    public static string RemoveUserMentions(this string str) => UserMentionsRegex.Replace(str, "");

    public static IEnumerable<ulong> GetUserMentions(this string str) => UserMentionsRegex.Matches(str)
        .Select(x => x.Groups["id"]).SelectMany(x => x.Captures).Select(x => ulong.TryParse(x.Value, out var u) ? u : 0)
        .Where(x => x is not 0).Distinct();

    public static string SanitizeAllMentions(this string? str) => str.SanitizeMentions().SanitizeRoleMentions();

    public static string ToBase64(this string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string GetInitials(this string txt, string glue = "") => string.Join(glue, txt.Split(' ').Select(x => x.FirstOrDefault()));

    public static bool IsAlphaNumeric(this string txt) => txt.All(c => LettersAndDigits.Contains(c));

    public static string RemoveUrls(this string txt) => Extensions.UrlRegex.Replace(txt, "");

    public static string EscapeWeirdStuff(this string txt) => txt.Replace(@"\", @"\\").Replace("\"", "\\\"");

    public static bool TryFormat(string data, object[] args, out string output)
    {
        output = null;
        try
        {
            output = string.Format(data, args);
            return true;
        }
        catch
        {
            return false;
        }
    }
}