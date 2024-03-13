using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Common;

public static class SmartEmbed
{
    public static bool TryParse(
        string input,
        ulong? guildId,
        out Discord.Embed[]? embeds,
        out string? plainText,
        out ComponentBuilder? components)
    {
        try
        {
            components = null;
            plainText = null;
            embeds = null;
            NewEmbed newEmbed;
            try
            {
                newEmbed = JsonConvert.DeserializeObject<NewEmbed>(input);
            }
            catch
            {
                return false;
            }


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
        catch
        {
            Log.Error("Unable to parse embed");
            embeds = null;
            plainText = null;
            components = null;
            return false;
        }
    }
}