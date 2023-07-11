using System.Net;
using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using PokeApiNet;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class PokemonCommands : MewdekoSubmodule
    {
        private readonly PokeApiClient pokeClient = new();
        private readonly InteractiveService interactivity;

        public PokemonCommands(InteractiveService interactivity)
        {
            this.interactivity = interactivity;
        }

        [Cmd, Aliases]
        public async Task Pokemon([Remainder] string name)
        {
            var isShiny = false;
            Pokemon? poke;

            if (name.ToLower().Contains("shiny"))
            {
                isShiny = true;
                name = name.ToLower().Replace("shiny", "");
            }

            try
            {
                poke = await pokeClient.GetResourceAsync<Pokemon>(name.Replace(" ", "")).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                await ReplyAsync("Seems like that Pokémon wasn't found. Please try again with a different query!").ConfigureAwait(false);
                return;
            }

            var abilities = new List<Ability>();
            if (poke.Abilities.Any())
            {
                foreach (var i in poke.Abilities)
                {
                    var ability = await pokeClient.GetResourceAsync<Ability>(i.Ability.Name).ConfigureAwait(false);
                    abilities.Add(ability);
                }
            }

            var stats = poke.Stats.Select(s => $"{s.Stat.Name}: {s.BaseStat}").ToList();


            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(poke.Moves.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            Task<PageBuilder> PageFactory(int page)
            {
                var pb = new PageBuilder();
                pb.WithTitle(isShiny ? $"Shiny {char.ToUpper(poke.Name[0]) + poke.Name[1..]}" : char.ToUpper(poke.Name[0]) + poke.Name[1..]);
                pb.WithOkColor();
                pb.AddField("Abilities", string.Join("\n", abilities.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));

                if (poke.Forms.Any())
                {
                    pb.AddField("Forms", string.Join("\n", poke.Forms.Select(x => $"{char.ToUpper(x.Name[0]) + x.Name[1..]}")));
                }

                if (stats.Any())
                {
                    pb.AddField("Base Stats", string.Join("\n", stats));
                }

                if (poke.Types.Any())
                {
                    pb.AddField("Types", string.Join("\n", poke.Types.Select(x => $"{char.ToUpper(x.Type.Name[0]) + x.Type.Name[1..]}")));
                }

                if (poke.HeldItems.Any())
                {
                    pb.AddField("Held Items", string.Join("\n", poke.HeldItems.Select(x => $"{char.ToUpper(x.Item.Name[0]) + x.Item.Name[1..]}")));
                }

                if (poke.GameIndicies.Any())
                {
                    pb.AddField("Game Indices", string.Join("\n", poke.GameIndicies.Skip(10 * 1).Take(10).Select(x => $"{char.ToUpper(x.Version.Name[0]) + x.Version.Name[1..]}")));
                }

                if (poke.Moves.Any())
                {
                    pb.AddField("Moves", string.Join("\n", poke.Moves.Skip(10 * page).Take(10).Select(x => $"{char.ToUpper(x.Move.Name[0]) + x.Move.Name[1..]}")));
                }

                pb.WithImageUrl(isShiny ? $"http://img.dscord.co/shiny/{poke.Id}-0-.png" : $"http://img.dscord.co/images/{poke.Id}-0-.png");

                return Task.FromResult(pb);
            }
        }
    }
}