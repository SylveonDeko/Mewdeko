using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using PokeApiNet;
using System.Net;
using System.Net.Http;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class PokemonCommands : MewdekoSubmodule
    {
        private readonly PokeApiClient _pokeClient = new();

        [Cmd, Aliases]
        public async Task Pokemon([Remainder] string name)
        {
            var isShiny = false;
            Pokemon poke = null;
            if (name.ToLower().Contains("shiny"))
            {
                isShiny = true;
                name = name.ToLower().Replace("shiny", "");
            }

            try
            {
                poke = await _pokeClient.GetResourceAsync<Pokemon>(name.Replace(" ", ""));
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    await ctx.Channel.SendErrorAsync(
                        "Seems like that pokemon wasn't found. Please try again with a different query!");
                    return;
                }
            }

            var abilities = new List<Ability>();
            if (poke.Abilities.Any())
                foreach (var i in poke.Abilities)
                {
                    var ability = await _pokeClient.GetResourceAsync<Ability>(i.Ability.Name);
                    abilities.Add(ability);
                }

            if (isShiny)
            {
                var eb = new EmbedBuilder();
                eb.WithTitle($"Shiny {char.ToUpper(poke.Name[0]) + poke.Name[1..]}");
                eb.WithOkColor();
                eb.AddField("Abilites",
                    string.Join("\n", abilities.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
                if (poke.Forms.Any() || poke.Forms.Count != 1)
                    eb.AddField("Forms",
                        string.Join("\n", poke.Forms.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
                eb.WithThumbnailUrl(poke.Sprites.FrontShiny);
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
            else
            {
                var eb = new EmbedBuilder();
                eb.WithTitle(char.ToUpper(poke.Name[0]) + poke.Name[1..]);
                eb.WithOkColor();
                eb.AddField("Abilites",
                    string.Join("\n", abilities.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
                if (poke.Forms.Any() || poke.Forms.Count != 1)
                    eb.AddField("Forms",
                        string.Join("\n", poke.Forms.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
                eb.WithThumbnailUrl(poke.Sprites.FrontDefault);
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
        }
    }
}