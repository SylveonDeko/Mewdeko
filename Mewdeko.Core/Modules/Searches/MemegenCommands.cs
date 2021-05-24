using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches
{
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

            public MemegenCommands(IHttpClientFactory factory)
            {
                _httpFactory = factory;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Memelist(int page = 1)
            {
                if (--page < 0)
                    return;

                using (var http = _httpFactory.CreateClient("memelist"))
                {
                    var res = await http.GetAsync("https://memegen.link/api/templates/")
                        .ConfigureAwait(false);

                    var rawJson = await res.Content.ReadAsStringAsync();

                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(rawJson)
                        .Select(kvp => Path.GetFileName(kvp.Value))
                        .ToList();

                    await ctx.SendPaginatedConfirmAsync(page, curPage =>
                    {
                        var embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(string.Join('\n', data.Skip(curPage * 20).Take(20)));

                        return embed;
                    }, data.Count, 20);
                    //await ctx.Channel.SendTableAsync(data, x => $"{x,-15}", 3).ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Memegen(string meme, string topText, string botText)
            {
                var top = Replace(topText);
                var bot = Replace(botText);
                await ctx.Channel.SendMessageAsync($"http://memegen.link/{meme}/{top}/{bot}.jpg")
                    .ConfigureAwait(false);
            }

            private static string Replace(string input)
            {
                var sb = new StringBuilder();

                foreach (var c in input)
                    if (_map.TryGetValue(c, out var tmp))
                        sb.Append(tmp);
                    else
                        sb.Append(c);

                return sb.ToString();
            }
        }
    }
}