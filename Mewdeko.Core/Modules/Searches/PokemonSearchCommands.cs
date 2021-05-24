using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonSearchCommands : MewdekoSubmodule<SearchesService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Pokemon([Remainder] string pokemon = null)
            {
                var str = pokemon.Replace(" ", "-");
                var client = new HttpClient();
                var response = await client.GetAsync(
                    $"https://pokeapi.co/api/v2/pokemon/{str}");
                var responseContent = response.Content;
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    var stats = new List<string>();
                    var abil = new List<string>();
                    var er = await reader.ReadToEndAsync();
                    var stuff = JsonConvert.DeserializeObject<Pokemon>(er,
                        new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
                    foreach (var i in stuff.stats) stats.Add($"{i.stat.name.ToUpperInvariant()}: {i.base_stat}");

                    foreach (var i in stuff.abilities) abil.Add($"{i.ability.name}");

                    var eb = new EmbedBuilder();
                    eb.AddField("Pokemon Name", stuff.name);
                    eb.AddField("Pokemon Species", stuff.species.name);
                    eb.AddField("Pokemon Stats", string.Join("\n", stats));
                    eb.AddField("Abilities", string.Join("\n", abil));
                    eb.WithThumbnailUrl(stuff.sprites.front_default);
                    eb.WithOkColor();
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }
            }

            //[MewdekoCommand, Usage, Description, Aliases]
            //public async Task PokemonAbility([Remainder] string ability = null)
            //{

            //}
        }
    }
}