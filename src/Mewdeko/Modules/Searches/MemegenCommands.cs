using System.Collections.Immutable;
using System.Net.Http;
using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    /// Module for generating memes using the memegen API.
    /// </summary>
    [Group]
    public class MemegenCommands(IHttpClientFactory factory, InteractiveService serv) : MewdekoSubmodule
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

        /// <summary>
        /// Lists available meme templates.
        /// </summary>
        /// <remarks>
        /// This command retrieves a list of available meme templates from the memegen API and displays them in a paginated embed.
        /// </remarks>
        /// <example>
        /// <code>.memelist</code>
        /// </example>
        [Cmd, Aliases]
        public async Task Memelist()
        {
            using var http = factory.CreateClient("memelist");
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

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var templates = data.Skip(page * 15).Take(15).Aggregate("",
                    (current, template) => current + $"**{template.Name}:**\n key: `{template.Id}`\n");
                return new PageBuilder()
                    .WithOkColor()
                    .WithDescription(templates);
            }
        }


        /// <summary>
        /// Generates a meme with the specified template and text.
        /// </summary>
        /// <remarks>
        /// This command generates a meme using the specified template and text and sends it to the channel.
        /// </remarks>
        /// <param name="meme">The name of the meme template.</param>
        /// <param name="memeText">The text to include in the meme (optional).</param>
        /// <example>
        /// <code>.memegen spongebob "this is a meme;with text"</code>
        /// </example>
        [Cmd, Aliases]
        public async Task Memegen(string meme, [Remainder] string? memeText = null)
        {
            var memeUrl = $"https://api.memegen.link/{meme}";
            if (!string.IsNullOrWhiteSpace(memeText))
            {
                memeUrl = memeText.Split(';').Select(Replace)
                    .Aggregate(memeUrl, (current, newText) => current + $"/{newText}");
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

        private class MemegenTemplate(string name, string id)
        {
            public string Name { get; } = name;
            public string Id { get; } = id;
        }
    }
}