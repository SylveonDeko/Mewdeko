using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.Pokemon;
using Mewdeko.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class PokemonSearchCommands : MewdekoSubmodule<SearchesService>
        {
            private readonly IDataCache _cache;

            public PokemonSearchCommands(IDataCache cache)
            {
                _cache = cache;
            }

            public IReadOnlyDictionary<string, SearchPokemon> Pokemons => _cache.LocalData.Pokemons;

            public IReadOnlyDictionary<string, SearchPokemonAbility> PokemonAbilities =>
                _cache.LocalData.PokemonAbilities;

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Pokemon([Remainder] string pokemon = null)
            {
                pokemon = pokemon?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(pokemon))
                    return;

                foreach (var kvp in Pokemons)
                    if (kvp.Key.ToUpperInvariant() == pokemon.ToUpperInvariant())
                    {
                        var p = kvp.Value;
                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithTitle(kvp.Key.ToTitleCase())
                                .WithDescription(p.BaseStats.ToString())
                                .WithThumbnailUrl(
                                    $"https://assets.pokemon.com/assets/cms2/img/pokedex/detail/{p.Id.ToString("000")}.png")
                                .AddField(efb =>
                                    efb.WithName(GetText("types")).WithValue(string.Join("\n", p.Types))
                                        .WithIsInline(true))
                                .AddField(efb =>
                                    efb.WithName(GetText("height_weight"))
                                        .WithValue(GetText("height_weight_val", p.HeightM, p.WeightKg))
                                        .WithIsInline(true))
                                .AddField(efb =>
                                    efb.WithName(GetText("abilities"))
                                        .WithValue(string.Join("\n", p.Abilities.Select(a => a.Value)))
                                        .WithIsInline(true)))
                            .ConfigureAwait(false);
                        return;
                    }

                await ReplyErrorLocalizedAsync("pokemon_none").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task PokemonAbility([Remainder] string ability = null)
            {
                ability = ability?.Trim().ToUpperInvariant().Replace(" ", "", StringComparison.InvariantCulture);
                if (string.IsNullOrWhiteSpace(ability))
                    return;
                foreach (var kvp in PokemonAbilities)
                    if (kvp.Key.ToUpperInvariant() == ability)
                    {
                        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle(kvp.Value.Name)
                            .WithDescription(string.IsNullOrWhiteSpace(kvp.Value.Desc)
                                ? kvp.Value.ShortDesc
                                : kvp.Value.Desc)
                            .AddField(efb => efb.WithName(GetText("rating"))
                                .WithValue(kvp.Value.Rating.ToString(_cultureInfo)).WithIsInline(true))
                        ).ConfigureAwait(false);
                        return;
                    }

                await ReplyErrorLocalizedAsync("pokemon_ability_none").ConfigureAwait(false);
            }
        }
    }
}