using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class MemegenCommands : MewdekoSubmodule
    {
        private static readonly ImmutableDictionary<char, string> _map = new Dictionary<char, string>
        {
            {'?', "~q"},
            {'%', "~p"},
            {'#', "~h"},
            {'/', "~s"},
            {' ', "-"},
            {'-', "--"},
            {'_', "__"},
            {'"', "''"}
        }.ToImmutableDictionary();

        private readonly IHttpClientFactory _httpFactory;
        private readonly InteractiveService _interactivity;

        public MemegenCommands(IHttpClientFactory factory, InteractiveService serv)
        {
            _interactivity = serv;
            _httpFactory = factory;
        }

        [Cmd, Aliases]
        public async Task Memelist()
        {
            using var http = _httpFactory.CreateClient("memelist");
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

            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
            var memeUrl = $"http://api.memegen.link/{meme}";
            if (!string.IsNullOrWhiteSpace(memeText))
            {
                foreach (var text in memeText.Split(';'))
                {
                    var newText = Replace(text);
                    memeUrl += $"/{newText}";
                }
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
                if (_map.TryGetValue(c, out var tmp))
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