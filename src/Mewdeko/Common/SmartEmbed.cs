using Discord;
using Mewdeko.Database.Extensions;
using Newtonsoft.Json;

namespace Mewdeko.Common;

public class SmartEmbed
{
    public static bool TryParse(string input, out EmbedBuilder? embed, out string plainText)
    {
        CrEmbed crembed;
        embed = null;
        plainText = string.Empty;
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

            if (newEmbed.Embed.Fields is { Count: > 0 })
            {
                foreach (var f in newEmbed.Embed.Fields)
                {
                    f.Name = f.Name.TrimTo(256);
                    f.Value = f.Value.TrimTo(1024);
                }
            }

            if (newEmbed is { IsValid: false })
                return false;

            embed = !newEmbed.IsEmbedValid ? null : newEmbed.ToEmbed();
            plainText = newEmbed.Content;
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

        embed = !crembed.IsEmbedValid ? null : crembed.ToEmbed();
        plainText = crembed.PlainText;
        return true;
    }
}