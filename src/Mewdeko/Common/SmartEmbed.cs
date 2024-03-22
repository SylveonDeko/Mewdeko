using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Common
{
    /// <summary>
    /// Provides methods for parsing and creating Discord embeds using embed json found at https://eb.mewdeko.tech
    /// </summary>
    public static class SmartEmbed
    {
        /// <summary>
        /// Tries to parse the input string into Discord embeds, plain text, and components.
        /// </summary>
        /// <param name="input">The input string containing the embed data.</param>
        /// <param name="guildId">The ID of the guild where the embed is parsed.</param>
        /// <param name="embeds">When this method returns, contains the parsed Discord embeds, if parsing succeeds; otherwise, null.</param>
        /// <param name="plainText">When this method returns, contains the parsed plain text, if parsing succeeds; otherwise, null.</param>
        /// <param name="components">When this method returns, contains the parsed components, if parsing succeeds; otherwise, null.</param>
        /// <returns><c>true</c> if the input string is successfully parsed into Discord embeds, plain text, and components; otherwise, <c>false</c>.</returns>
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
}