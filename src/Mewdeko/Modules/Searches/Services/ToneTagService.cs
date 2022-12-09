using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Searches.Services;

public class ToneTagService
{
    private readonly Regex toneTagRegex = new(@"(?:\/|\\)([^\\\/ ]*)", RegexOptions.Compiled);
    private readonly IBotStrings strings;
    public IReadOnlyList<ToneTag> Tags { get; private set; }

    public ToneTagService(IBotStrings strings, BotConfigService bss) =>
        (this.strings, _, Tags) = (strings, bss, JsonSerializer.Deserialize<List<ToneTag>>(File.ReadAllText("data/tags.json")));

    public List<string> GetToneTags(string input) =>
        toneTagRegex.Matches(input.RemoveUrls()).Select(x => x.Value[1..]).ToList();

    public ParseResult ParseTags(List<string> rawTags)
    {
        var tags = rawTags.DistinctBy(x => x.ToLower());
        List<ToneTag> success = new();
        List<string> fails = new();

        tags.ForEach(s =>
        {
            var tt = Tags.FirstOrDefault(tag => tag.GetAllValues().Contains(s));
            if (tt is null) fails.Add(s);
            else success.Add(tt);
        });

        return new ParseResult(success, rawTags.Where(x => !fails.Contains(x)).ToList(), fails);
    }

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

    public static string GetMarkdownLink(ToneTagSource source) =>
        !string.IsNullOrWhiteSpace(source.Url) ? $"[{source.Title}]({source.Url})" : source.Title;

    public ParseResult ParseTags(string input) => ParseTags(GetToneTags(input));

    public record ParseResult(List<ToneTag> Tags, List<string> ActualTags, List<string> MissingTags);
}
