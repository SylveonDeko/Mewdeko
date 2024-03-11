using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Mewdeko.Common.Yml;
using Newtonsoft.Json;

namespace Mewdeko.Extensions;

public static partial class StringExtensions
{
    private static readonly HashSet<char> LettersAndDigits =
    [
        ..Enumerable.Range(48, 10)
            .Concat(Enumerable.Range(65, 26))
            .Concat(Enumerable.Range(97, 26))
            .Select(x => (char)x)
    ];

    private static readonly Regex FilterRegex =
        MyRegex1();

    private static readonly Regex CodePointRegex
        = MyRegex2();

    private static readonly Dictionary<string, string> ColorNameHexMapping = new()
    {
        {
            "AliceBlue", "#F0F8FF"
        },
        {
            "AntiqueWhite", "#FAEBD7"
        },
        {
            "Aqua", "#00FFFF"
        },
        {
            "Aquamarine", "#7FFFD4"
        },
        {
            "Azure", "#F0FFFF"
        },
        {
            "Beige", "#F5F5DC"
        },
        {
            "Bisque", "#FFE4C4"
        },
        {
            "Black", "#000000"
        },
        {
            "BlanchedAlmond", "#FFEBCD"
        },
        {
            "Blue", "#0000FF"
        },
        {
            "BlueViolet", "#8A2BE2"
        },
        {
            "Brown", "#A52A2A"
        },
        {
            "BurlyWood", "#DEB887"
        },
        {
            "CadetBlue", "#5F9EA0"
        },
        {
            "Chartreuse", "#7FFF00"
        },
        {
            "Chocolate", "#D2691E"
        },
        {
            "Coral", "#FF7F50"
        },
        {
            "CornflowerBlue", "#6495ED"
        },
        {
            "Cornsilk", "#FFF8DC"
        },
        {
            "Crimson", "#DC143C"
        },
        {
            "Cyan", "#00FFFF"
        },
        {
            "DarkBlue", "#00008B"
        },
        {
            "DarkCyan", "#008B8B"
        },
        {
            "DarkGoldenRod", "#B8860B"
        },
        {
            "DarkGray", "#A9A9A9"
        },
        {
            "DarkGreen", "#006400"
        },
        {
            "DarkKhaki", "#BDB76B"
        },
        {
            "DarkMagenta", "#8B008B"
        },
        {
            "DarkOliveGreen", "#556B2F"
        },
        {
            "DarkOrange", "#FF8C00"
        },
        {
            "DarkOrchid", "#9932CC"
        },
        {
            "DarkRed", "#8B0000"
        },
        {
            "DarkSalmon", "#E9967A"
        },
        {
            "DarkSeaGreen", "#8FBC8F"
        },
        {
            "DarkSlateBlue", "#483D8B"
        },
        {
            "DarkSlateGray", "#2F4F4F"
        },
        {
            "DarkTurquoise", "#00CED1"
        },
        {
            "DarkViolet", "#9400D3"
        },
        {
            "DeepPink", "#FF1493"
        },
        {
            "DeepSkyBlue", "#00BFFF"
        },
        {
            "DimGray", "#696969"
        },
        {
            "DodgerBlue", "#1E90FF"
        },
        {
            "FireBrick", "#B22222"
        },
        {
            "FloralWhite", "#FFFAF0"
        },
        {
            "ForestGreen", "#228B22"
        },
        {
            "Fuchsia", "#FF00FF"
        },
        {
            "Gainsboro", "#DCDCDC"
        },
        {
            "GhostWhite", "#F8F8FF"
        },
        {
            "Gold", "#FFD700"
        },
        {
            "GoldenRod", "#DAA520"
        },
        {
            "Gray", "#808080"
        },
        {
            "Green", "#008000"
        },
        {
            "GreenYellow", "#ADFF2F"
        },
        {
            "HoneyDew", "#F0FFF0"
        },
        {
            "HotPink", "#FF69B4"
        },
        {
            "IndianRed", "#CD5C5C"
        },
        {
            "Indigo", "#4B0082"
        },
        {
            "Ivory", "#FFFFF0"
        },
        {
            "Khaki", "#F0E68C"
        },
        {
            "Lavender", "#E6E6FA"
        },
        {
            "LavenderBlush", "#FFF0F5"
        },
        {
            "LawnGreen", "#7CFC00"
        },
        {
            "LemonChiffon", "#FFFACD"
        },
        {
            "LightBlue", "#ADD8E6"
        },
        {
            "LightCoral", "#F08080"
        },
        {
            "LightCyan", "#E0FFFF"
        },
        {
            "LightGoldenRodYellow", "#FAFAD2"
        },
        {
            "LightGray", "#D3D3D3"
        },
        {
            "LightGreen", "#90EE90"
        },
        {
            "LightPink", "#FFB6C1"
        },
        {
            "LightSalmon", "#FFA07A"
        },
        {
            "LightSeaGreen", "#20B2AA"
        },
        {
            "LightSkyBlue", "#87CEFA"
        },
        {
            "LightSlateGray", "#778899"
        },
        {
            "LightSteelBlue", "#B0C4DE"
        },
        {
            "LightYellow", "#FFFFE0"
        },
        {
            "Lime", "#00FF00"
        },
        {
            "LimeGreen", "#32CD32"
        },
        {
            "Linen", "#FAF0E6"
        },
        {
            "Magenta", "#FF00FF"
        },
        {
            "Maroon", "#800000"
        },
        {
            "MediumAquaMarine", "#66CDAA"
        },
        {
            "MediumBlue", "#0000CD"
        },
        {
            "MediumOrchid", "#BA55D3"
        },
        {
            "MediumPurple", "#9370DB"
        },
        {
            "MediumSeaGreen", "#3CB371"
        },
        {
            "MediumSlateBlue", "#7B68EE"
        },
        {
            "MediumSpringGreen", "#00FA9A"
        },
        {
            "MediumTurquoise", "#48D1CC"
        },
        {
            "MediumVioletRed", "#C71585"
        },
        {
            "MidnightBlue", "#191970"
        },
        {
            "MintCream", "#F5FFFA"
        },
        {
            "MistyRose", "#FFE4E1"
        },
        {
            "Moccasin", "#FFE4B5"
        },
        {
            "NavajoWhite", "#FFDEAD"
        },
        {
            "Navy", "#000080"
        },
        {
            "OldLace", "#FDF5E6"
        },
        {
            "Olive", "#808000"
        },
        {
            "OliveDrab", "#6B8E23"
        },
        {
            "Orange", "#FFA500"
        },
        {
            "OrangeRed", "#FF4500"
        },
        {
            "Orchid", "#DA70D6"
        },
        {
            "PaleGoldenRod", "#EEE8AA"
        },
        {
            "PaleGreen", "#98FB98"
        },
        {
            "PaleTurquoise", "#AFEEEE"
        },
        {
            "PaleVioletRed", "#DB7093"
        },
        {
            "PapayaWhip", "#FFEFD5"
        },
        {
            "PeachPuff", "#FFDAB9"
        },
        {
            "Peru", "#CD853F"
        },
        {
            "Pink", "#FFC0CB"
        },
        {
            "Plum", "#DDA0DD"
        },
        {
            "PowderBlue", "#B0E0E6"
        },
        {
            "Purple", "#800080"
        },
        {
            "RebeccaPurple", "#663399"
        },
        {
            "Red", "#FF0000"
        },
        {
            "RosyBrown", "#BC8F8F"
        },
        {
            "RoyalBlue", "#4169E1"
        },
        {
            "SaddleBrown", "#8B4513"
        },
        {
            "Salmon", "#FA8072"
        },
        {
            "SandyBrown", "#F4A460"
        },
        {
            "SeaGreen", "#2E8B57"
        },
        {
            "SeaShell", "#FFF5EE"
        },
        {
            "Sienna", "#A0522D"
        },
        {
            "Silver", "#C0C0C0"
        },
        {
            "SkyBlue", "#87CEEB"
        },
        {
            "SlateBlue", "#6A5ACD"
        },
        {
            "SlateGray", "#708090"
        },
        {
            "Snow", "#FFFAFA"
        },
        {
            "SpringGreen", "#00FF7F"
        },
        {
            "SteelBlue", "#4682B4"
        },
        {
            "Tan", "#D2B48C"
        },
        {
            "Teal", "#008080"
        },
        {
            "Thistle", "#D8BFD8"
        },
        {
            "Tomato", "#FF6347"
        },
        {
            "Turquoise", "#40E0D0"
        },
        {
            "Violet", "#EE82EE"
        },
        {
            "Wheat", "#F5DEB3"
        },
        {
            "White", "#FFFFFF"
        },
        {
            "WhiteSmoke", "#F5F5F5"
        },
        {
            "Yellow", "#FFFF00"
        },
        {
            "YellowGreen", "#9ACD32"
        }
    };

    public static string? GetHexFromColorName(string colorName)
    {
        return (ColorNameHexMapping.TryGetValue(colorName, out var hexValue) ? hexValue : null) ?? string.Empty;
    }

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

    public static readonly Regex UserMentionsRegex = MyRegex();
    public static bool IsNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str);

    public static string PadBoth(this string str, int length)
    {
        var spaces = length - str.Length;
        var padLeft = spaces / 2 + str.Length;
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
        => input.EndsWith("gifv") || input.EndsWith("mp4") || !input.EndsWith(".png") && !input.EndsWith(".jpg") &&
            !input.EndsWith(".jpeg") || !input.EndsWith(".gif");

    public static T MapJson<T>(this string str) => JsonConvert.DeserializeObject<T>(str);

    public static string StripHtml(this string input) => Regex.Replace(input, "<.*?>", string.Empty);

    public static string ToTitleCase(this string str)
    {
        var tokens = str.Split([
            " "
        ], StringSplitOptions.RemoveEmptyEntries);
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

    public static string SanitizeRoleMentions(this string? str) =>
        str.Replace("<@&", "<ම&", StringComparison.InvariantCultureIgnoreCase);

    public static string RemoveUserMentions(this string str) => UserMentionsRegex.Replace(str, "");

    public static IEnumerable<ulong> GetUserMentions(this string str) => UserMentionsRegex.Matches(str)
        .Select(x => ulong.TryParse(x.Value, out var u) ? u : 0)
        .Where(x => x is not 0).Distinct();

    public static string SanitizeAllMentions(this string? str) => str.SanitizeMentions().SanitizeRoleMentions();

    public static string ToBase64(this string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string GetInitials(this string txt, string glue = "") =>
        string.Join(glue, txt.Split(' ').Select(x => x.FirstOrDefault()));

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

    [GeneratedRegex(@"@<@\d{17,19}>|\d{17,19}", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"discord(?:\.gg|\.io|\.me|\.li|(?:app)?\.com\/invite)\/(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex1();

    [GeneratedRegex(@"(\\U(?<code>[a-zA-Z0-9]{8})|\\u(?<code>[a-zA-Z0-9]{4})|\\x(?<code>[a-zA-Z0-9]{2}))",
        RegexOptions.Compiled)]
    private static partial Regex MyRegex2();
}