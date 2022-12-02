namespace Mewdeko.Common;

public class ToneTag
{
    public ToneTagSource? Source { get; set; }
    public string DefaultName { get; set; }
    public string DefaultShortName { get; set; }
    public string Description { get; set; }
    public List<string> Aliases { get; set; }
    public List<string> ShortAliases { get; set; }

    public List<string> GetAllValues()
        => Aliases.Concat(ShortAliases).Append(DefaultName).Append(DefaultShortName).ToList();
}

public record ToneTagSource(string Title, string? Url);