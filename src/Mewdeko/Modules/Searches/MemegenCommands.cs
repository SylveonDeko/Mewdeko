using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class MemegenCommands : MewdekoSubmodule
    {
        private static readonly ImmutableDictionary<char, string> Map = new Dictionary<char, string>
        {
            {
                '?', "~q"
            },
            {
                '%', "~p"
            },
            {
                '#', "~h"
            },
            {
                '/', "~s"
            },
            {
                ' ', "-"
            },
            {
                '-', "--"
            },
            {
                '_', "__"
            },
            {
                '"', "''"
            }
        }.ToImmutableDictionary();

        private readonly IHttpClientFactory httpFactory;
        private readonly InteractiveService interactivity;

        public MemegenCommands(IHttpClientFactory factory, InteractiveService serv)
        {
            interactivity = serv;
            httpFactory = factory;
        }

        [Cmd, Aliases]
        public async Task Memelist()
        {
            using var http = httpFactory.CreateClient("memelist");
            var res = await http.GetAsync("https://api.memegen.link/templates/")
                .ConfigureAwait(false);

            var rawJson = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

            var data = JsonConvert.DeserializeObject<List<MemegenTemplate>>(rawJson);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(data.Count / 15)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var templates = data.Skip(page * 15).Take(15).Aggregate("", (current, template) => current + $"**{template.Name}:**\n key: `{template.Id}`\n");
                return new PageBuilder()
                    .WithOkColor()
                    .WithDescription(templates);
            }
        }

        [Cmd, Aliases]
        public async Task Memegen(string meme, [Remainder] string? memeText = null)
        {
            var memeUrl = $"https://api.memegen.link/{meme}";
            if (!string.IsNullOrWhiteSpace(memeText))
            {
                memeUrl = memeText.Split(';').Select(Replace).Aggregate(memeUrl, (current, newText) => current + $"/{newText}");
            }

            memeUrl += ".png";
            await ctx.Channel.SendMessageAsync(memeUrl)
                .ConfigureAwait(false);
        }

        private static string Replace(string input)
        {
            var sb = new StringBuilder();

            foreach (var c in input)
            {
                if (Map.TryGetValue(c, out var tmp))
                    sb.Append(tmp);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private class MemegenTemplate
        {
            public MemegenTemplate(string name, string id)
            {
                Name = name;
                Id = id;
            }

            public string Name { get; }
            public string Id { get; }
        }
    }
}