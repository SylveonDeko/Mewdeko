using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service for handling tone tags.
/// </summary>
public class ToneTagService
{
    private readonly IBotStrings strings;
    private readonly Regex toneTagRegex = new(@"(?:\/|\\)([^\\\/ ]*)", RegexOptions.Compiled);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToneTagService" /> class.
    /// </summary>
    /// <param name="strings">The bot strings service instance.</param>
    /// <param name="bss">The bot configuration service instance.</param>
    public ToneTagService(IBotStrings strings, BotConfigService bss)
    {
        (this.strings, _, Tags) = (strings, bss,
            JsonSerializer.Deserialize<List<ToneTag>>(File.ReadAllText("data/tags.json")));
    }


    /// <summary>
    ///     Gets the list of tone tags.
    /// </summary>
    public IReadOnlyList<ToneTag> Tags { get; }

    /// <summary>
    ///     Parses tone tags from the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>A list of tone tags parsed from the input.</returns>
    public List<string> GetToneTags(string input)
    {
        return toneTagRegex.Matches(input.RemoveUrls()).Select(x => x.Value[1..]).ToList();
    }

    /// <summary>
    ///     Parses tone tags from raw tag strings.
    /// </summary>
    /// <param name="rawTags">The list of raw tag strings.</param>
    /// <returns>The parsing result containing successfully parsed tags, actual tag strings, and missing tags.</returns>
    private ParseResult ParseTags(IReadOnlyCollection<string> rawTags)
    {
        var tags = rawTags.DistinctBy(x => x.ToLower());
        List<ToneTag> success = [];
        List<string> fails = [];

        tags.ForEach(s =>
        {
            var tt = Tags.FirstOrDefault(tag => tag.GetAllValues().Contains(s));
            if (tt is null) fails.Add(s);
            else success.Add(tt);
        });

        return new ParseResult(success, rawTags.Where(x => !fails.Contains(x)).ToList(), fails);
    }

    /// <summary>
    ///     Gets an embed builder representing the parsing result.
    /// </summary>
    /// <param name="result">The parsing result.</param>
    /// <param name="guild">The guild for which to get the embed.</param>
    /// <returns>An embed builder representing the parsing result.</returns>
    public EmbedBuilder GetEmbed(ParseResult result, IGuild? guild = null)
    {
        var eb = new EmbedBuilder()
            .WithFooter(strings.GetText("tonetags_upsell", guild?.Id));

        if (result.Tags.Count + result.MissingTags.Count == 0)
        {
            eb.WithTitle(strings.GetText("tonetags_none", guild?.Id))
                .WithDescription(strings.GetText("tonetags_none_body", guild?.Id)).WithErrorColor();
        }
        else if (result.Tags.Count == 1 && result.MissingTags.Count == 0)
        {
            var tag = result.Tags.First();
            eb.WithTitle($"/{result.ActualTags.First()}").WithDescription(tag.Description)
                .AddField(strings.GetText("tonetags_source", guild?.Id), GetMarkdownLink(tag.Source))
                .AddField(strings.GetText("tonetags_aliases", guild?.Id), string.Join(", ", tag.GetAllValues()))
                .WithOkColor();
        }
        else
        {
            eb.WithTitle(strings.GetText("tonetags_tonetags", guild?.Id));
            var i = -1;
            result.Tags.ForEach(x => eb.AddField(result.ActualTags[++i], x.Description));
            if (result.MissingTags.Count > 0)
            {
                eb.AddField(strings.GetText("tonetags_not_found", guild?.Id),
                    string.Join(", ", result.MissingTags.Distinct()));
            }

            eb.AddField(strings.GetText("tonetags_sources", guild?.Id),
                string.Join(", ", result.Tags.Select(x => GetMarkdownLink(x.Source)).Distinct()));
            eb.WithOkColor();
        }

        return eb;
    }

    /// <summary>
    ///     Gets a markdown-formatted link for a tone tag source.
    /// </summary>
    /// <param name="source">The tone tag source.</param>
    /// <returns>A markdown-formatted link for the tone tag source.</returns>
    public static string GetMarkdownLink(ToneTagSource source)
    {
        return !string.IsNullOrWhiteSpace(source.Url) ? $"[{source.Title}]({source.Url})" : source.Title;
    }

    /// <summary>
    ///     Parses tone tags from the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The parsing result containing successfully parsed tags, actual tag strings, and missing tags.</returns>
    public ParseResult ParseTags(string input)
    {
        return ParseTags(GetToneTags(input));
    }

    /// <summary>
    ///     Represents the parsing result of tone tags.
    /// </summary>
    public record ParseResult(List<ToneTag> Tags, List<string> ActualTags, List<string> MissingTags);
}