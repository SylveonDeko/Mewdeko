using System.Text.Json;
using Serilog;

namespace Mewdeko.Common;

/// <summary>
///     Provides methods for parsing and creating Discord embeds using embed json found at https://eb.mewdeko.tech
/// </summary>
public static class SmartEmbed
{
    /// <summary>
    ///     Tries to parse the input string into Discord embeds, plain text, and components.
    /// </summary>
    /// <param name="input">The input string containing the embed data.</param>
    /// <param name="guildId">The ID of the guild where the embed is parsed.</param>
    /// <param name="embeds">
    ///     When this method returns, contains the parsed Discord embeds, if parsing succeeds; otherwise,
    ///     null.
    /// </param>
    /// <param name="plainText">When this method returns, contains the parsed plain text, if parsing succeeds; otherwise, null.</param>
    /// <param name="components">
    ///     When this method returns, contains the parsed components, if parsing succeeds; otherwise,
    ///     null.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the input string is successfully parsed into Discord embeds, plain text, and components;
    ///     otherwise, <c>false</c>.
    /// </returns>
    public static bool TryParse(
        string input,
        ulong? guildId,
        out Discord.Embed[]? embeds,
        out string? plainText,
        out MessageComponent? components)
    {
        try
        {
            components = null;
            plainText = null;
            embeds = null;

            // Extract the first complete JSON object from the input
            var startIndex = input.IndexOf('{');
            if (startIndex == -1)
                return false;

            var jsonDepth = 0;
            var endIndex = -1;

            for (var i = startIndex; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '{':
                        jsonDepth++;
                        break;
                    case '}':
                        jsonDepth--;
                        if (jsonDepth == 0)
                        {
                            endIndex = i;
                            i = input.Length; // Break the loop
                        }
                        break;
                }
            }

            if (endIndex == -1 || jsonDepth != 0)
                return false;

            var jsonString = input.Substring(startIndex, endIndex - startIndex + 1);

            NewEmbed newEmbed;
            try
            {
                newEmbed = JsonSerializer.Deserialize<NewEmbed>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch(Exception ex)
            {
                Log.Information(ex, "Failed to parse JSON");
                return false;
            }
            
            // try new comps first, if it fails then do normal parsing 
            if (newEmbed.Containers is not null && newEmbed.Containers.Count >= 0)
            {
                ComponentBuilderV2 cv2b = new();
                foreach(var container in newEmbed.Containers)
                {
                    cv2b.WithContainer(container.GetBuilder(guildId, cv2b.Components.Count));
                }
                components = cv2b.Build();
                return true;
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
                embeds = NewEmbed.ToEmbedArray([
                    newEmbed.Embed
                ]);
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
            components = newEmbed.GetComponents(guildId).Build();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Unable to parse embed");
            embeds = null;
            plainText = null;
            components = null;
            return false;
        }
    }
}