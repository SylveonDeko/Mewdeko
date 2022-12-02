using Newtonsoft.Json;

namespace Mewdeko.Common;

public class SmartEmbed
{
    public static bool TryParse(
        string? input,
        ulong? guildId,
        out Discord.Embed[]? embeds,
        out string? plainText,
        out ComponentBuilder? components)
    {
        CrEmbed crembed;
        embeds = Array.Empty<Discord.Embed>();
        plainText = string.Empty;
        components = null;
        if (string.IsNullOrWhiteSpace(input) || !input.Trim().StartsWith('{')) return false;

        try
        {
            crembed = JsonConvert.DeserializeObject<CrEmbed>(input);
        }
        catch
        {
            return false;
        }

        if (!crembed.IsValid)
        {
            var newEmbed = JsonConvert.DeserializeObject<NewEmbed>(input);

            if (newEmbed.Embed?.Fields is { Count: > 0 })
            {
                foreach (var f in newEmbed.Embed.Fields)
                {
                    f.Name = f.Name.TrimTo(256);
                    f.Value = f.Value.TrimTo(1024);
                }
            }

            if (newEmbed.Embeds is not null && newEmbed.Embeds.Any(x => x.Fields is not null))
            {
                foreach (var f in newEmbed.Embeds.Select(x => x.Fields).Where(y => y is not null))
                {
                    foreach (var ff in f)
                    {
                        ff.Name = ff.Name.TrimTo(256);
                        ff.Value = ff.Value.TrimTo(1024);
                    }
                }
            }

            if (newEmbed is { IsValid: false })
                return false;

            if (newEmbed.Embed is not null)
            {
                embeds = NewEmbed.ToEmbedArray(new[]
                {
                    newEmbed.Embed
                });
            }
            else if (newEmbed.Embeds is not null && newEmbed.Embeds.Any())
            {
                embeds = NewEmbed.ToEmbedArray(newEmbed.Embeds);
            }
            else
            {
                embeds = null;
            }

            plainText = newEmbed.Content;
            components = newEmbed.GetComponents(guildId);
            return true;
        }

        if (crembed is { Fields.Length: > 0 })
        {
            foreach (var f in crembed.Fields)
            {
                f.Name = f.Name.TrimTo(256);
                f.Value = f.Value.TrimTo(1024);
            }
        }

        if (crembed is { IsValid: false })
            return false;

        embeds = new[]
        {
            crembed.ToEmbed().Build()
        };
        plainText = crembed.PlainText;
        components = crembed.GetComponents(guildId);
        return true;
    }
}