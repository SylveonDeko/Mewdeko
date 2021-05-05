using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NadekoBot.Common.Attributes;
using NadekoBot.Extensions;
using Newtonsoft.Json;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class MemegenCommands : NadekoSubmodule
        {
            private class MemegenTemplate
            {
                public string Name { get; set; }
                public string Id { get; set; }
            }
            private static readonly ImmutableDictionary<char, string> _map = new Dictionary<char, string>()
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

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Memelist(int page = 1)
            {
                if (--page < 0)
                    return;

                using (var http = _httpFactory.CreateClient("memelist"))
                {
                    var res = await http.GetAsync("https://api.memegen.link/templates/")
                        .ConfigureAwait(false);

                    var rawJson = await res.Content.ReadAsStringAsync();
                    
                    var data = JsonConvert.DeserializeObject<List<MemegenTemplate>>(rawJson);

                    await ctx.SendPaginatedConfirmAsync(page, curPage =>
                    {
                        var templates = "";
                        foreach (var template in data.Skip(curPage * 15).Take(15))
                        {
                            templates += $"**{template.Name}:**\n key: `{template.Id}`\n";
                        }
                        var embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(templates);

                        return embed;
                    }, data.Count, 15).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Memegen(string meme, [Leftover] string memeText = null)
            {
                var memeUrl = $"http://api.memegen.link/{meme}";
                if (!string.IsNullOrWhiteSpace(memeText))
                {
                    var memeTextArray = memeText.Split(';');
                    foreach(var text in memeTextArray)
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
        }
    }
}
