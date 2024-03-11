using Newtonsoft.Json;
using Serilog;

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
        try
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
            catch (Exception e)
            {
                Log.Error(e, "Failed to parse embed");
                return false;
            }

            if (!crembed.IsValid)
            {
                NewEmbed newEmbed;
                try
                {
                    newEmbed = JsonConvert.DeserializeObject<NewEmbed>(input);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to parse new format. trying dumb format");
                    try
                    {
                        var dumbEmbed = JsonConvert.DeserializeObject<DumbEmbed.DumbEmbed>(input);
                        embeds = DumbEmbed.DumbEmbed.ToEmbedArray(dumbEmbed.Embeds);
                        plainText = dumbEmbed.Content ?? string.Empty;
                        return true;
                    }
                    catch (Exception e2)
                    {
                        Log.Error(e2, "Failed to parse dumb format");
                        return false;
                    }
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

            embeds =
            [
                crembed.ToEmbed().Build()
            ];
            plainText = crembed.PlainText;
            components = crembed.GetComponents(guildId);
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Failed to parse embed: {E}", e);
            components = null;
            embeds = null;
            plainText = null;
            return false;
        }
    }
}