﻿using Discord;
using Discord.Commands;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.Pokemon;
using Mewdeko.Core.Services;
using System;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonSearchCommands : MewdekoSubmodule<SearchesService>
        {

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task Pokemon([Remainder] string pokemon = null)
            {
                var str = pokemon.Replace(" ", "-");
                var client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(
                    $"https://pokeapi.co/api/v2/pokemon/{str}");
                HttpContent responseContent = response.Content;
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    List<string> stats = new List<string>();
                    List<string> abil = new List<string>();
                    var er = await reader.ReadToEndAsync();
                    var stuff = JsonConvert.DeserializeObject<Pokemon>(er, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    foreach (var i in stuff.stats)
                    {
                        stats.Add($"{i.stat.name.ToUpperInvariant()}: {i.base_stat}");
                    }

                    foreach (var i in stuff.abilities)
                    {
                        abil.Add($"{i.ability.name}");
                    }

                    EmbedBuilder eb = new EmbedBuilder();
                    eb.AddField("Pokemon Name", stuff.name);
                    eb.AddField("Pokemon Species", stuff.species.name);
                    eb.AddField("Pokemon Stats", string.Join("\n", stats));
                    eb.AddField("Abilities", string.Join("\n",abil));
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
