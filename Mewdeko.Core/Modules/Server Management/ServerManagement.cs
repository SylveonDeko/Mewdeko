using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.ServerManagement.Services;

namespace Mewdeko.Modules.ServerManagement
{
    public partial class ServerManagement : MewdekoModule<ServerManagementService>
    {
        private readonly IHttpClientFactory _httpFactory;

        public IHttpClientFactory factory;

        public ServerManagement(IHttpClientFactory factory)
        {
            _httpFactory = factory;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task SetSplash(string img)
        {
            var guild = ctx.Guild;
            var uri = new Uri(img);
            using (var http = _httpFactory.CreateClient())
            using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using (var imgStream = imgData.ToStream())
                {
                    await guild.ModifyAsync(x => x.Splash = new Image(imgStream)).ConfigureAwait(false);
                    await ctx.Channel.SendMessageAsync("New splash image has been set!");
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task SetIcon(string img)
        {
            var guild = ctx.Guild;
            var uri = new Uri(img);
            using (var http = _httpFactory.CreateClient())
            using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using (var imgStream = imgData.ToStream())
                {
                    await guild.ModifyAsync(x => x.Icon = new Image(imgStream)).ConfigureAwait(false);
                    await ctx.Channel.SendMessageAsync("New server icon has been set!");
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task SetBanner(string img)
        {
            var guild = ctx.Guild;
            var uri = new Uri(img);
            using (var http = _httpFactory.CreateClient())
            using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using (var imgStream = imgData.ToStream())
                {
                    await guild.ModifyAsync(x => x.Banner = new Image(imgStream)).ConfigureAwait(false);
                    await ctx.Channel.SendMessageAsync("New server banner has been set!");
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task SetServerName([Remainder] string name)
        {
            var guild = ctx.Guild;
            await guild.ModifyAsync(x => { x.Name = name; });
            await ctx.Channel.SendMessageAsync("Succesfuly set server name to" + name);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageEmojis)]
        [BotPerm(GuildPerm.ManageEmojis)]
        [Priority(0)]
        public async Task AddEmote(string name, string url = null)
        {
            var guild = ctx.Guild;
            var acturl = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                var tags = ctx.Message.Attachments.FirstOrDefault();
                acturl = tags.Url;
            }
            else if (url.StartsWith("<"))
            {
                var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote) x.Value);
                var result = tags.Select(m => m.Url);
                acturl = string.Join("", result);
            }
            else
            {
                acturl = url;
            }

            var uri = new Uri(acturl);
            using var http = _httpFactory.CreateClient();
            using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using (var imgStream = imgData.ToStream())
                {
                    try
                    {
                        var emote = await ctx.Guild.CreateEmoteAsync(name, new Image(imgStream));
                        await ctx.Channel.SendConfirmAsync(emote + " with the name " + Format.Code(name) + " created!");
                    }
                    catch (Exception)
                    {
                        await ctx.Channel.SendErrorAsync(
                            "The emote could not be added because it is either: Too Big(Over 256kb), != a direct link, Or exceeds server emoji limit.");
                    }
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.ManageEmojis)]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveEmote(string emote)
        {
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote) x.Value)
                .FirstOrDefault();
            try
            {
                var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id);
                await ctx.Guild.DeleteEmoteAsync(emote1);
                await ctx.Channel.SendConfirmAsync($"{emote1} has been deleted!");
            }
            catch (HttpException)
            {
                await ctx.Channel.SendErrorAsync("This emote != from this guild!");
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.ManageEmojis)]
        [RequireContext(ContextType.Guild)]
        public async Task RenameEmote(string emote, string name)
        {
            if (name.StartsWith("<"))
            {
                await ctx.Channel.SendErrorAsync("You cant use an emote as a name!");
                return;
            }

            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote) x.Value)
                .FirstOrDefault();
            try
            {
                var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id);
                var ogname = emote1.Name;
                await ctx.Guild.ModifyEmoteAsync(emote1, x => { x.Name = name; });
                var emote2 = await ctx.Guild.GetEmoteAsync(tags.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"{emote1} has been renamed from {Format.Code(ogname)} to {Format.Code(emote2.Name)}");
            }
            catch (HttpException)
            {
                await ctx.Channel.SendErrorAsync("This emote != from this guild!");
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageEmojis)]
        [BotPerm(GuildPerm.ManageEmojis)]
        [Priority(1)]
        public async Task StealEmotes([Remainder] string e)
        {
            var eb = new EmbedBuilder
            {
                Description = "<a:loading:847706744741691402> Adding Emotes...",
                Color = Mewdeko.OkColor
            };
            var errored = new List<string>();
            var emotes = new List<string>();
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote) x.Value);
            if (!tags.Any()) return;
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
            foreach (var i in tags)
            {
                using var http = _httpFactory.CreateClient();
                using (var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false))
                {
                    var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    using (var imgStream = imgData.ToStream())
                    {
                        {
                            try
                            {
                                var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream));
                                emotes.Add($"{emote} {Format.Code(emote.Name)}");
                            }
                            catch (Exception)
                            {
                                errored.Add($"{i.Name}\n{i.Url}");
                            }
                        }
                    }
                }
            }

            var b = new EmbedBuilder();
            b.Color = Mewdeko.OkColor;
            if (emotes.Any()) b.WithDescription($"**Added Emotes**\n{string.Join("\n", emotes)}");
            if (errored.Any()) b.AddField("Errored Emotes", string.Join("\n\n", errored));
            await msg.ModifyAsync(x => { x.Embed = b.Build(); });
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageEmojis)]
        [BotPerm(GuildPerm.ManageEmojis)]
        [Priority(0)]
        public async Task StealForRole(IRole role, [Remainder] string e)
        {
            var eb = new EmbedBuilder
            {
                Description = $"<a:loading:847706744741691402> Adding Emotes to {role.Mention}...",
                Color = Mewdeko.OkColor
            };
            var list = new Optional<IEnumerable<IRole>>(new[] { role });
            var errored = new List<string>();
            var emotes = new List<string>();
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value);
            if (!tags.Any()) return;
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());

            foreach (var i in tags)
            {
                using var http = _httpFactory.CreateClient();
                using (var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false))
                {
                    var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    using (var imgStream = imgData.ToStream())
                    {
                        {
                            try
                            {
                                var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream), list);
                                emotes.Add($"{emote} {Format.Code(emote.Name)}");
                            }
                            catch (Exception)
                            {
                                errored.Add($"{i.Name}\n{i.Url}");
                            }
                        }
                    }
                }
            }

            var b = new EmbedBuilder();
            b.Color = Mewdeko.OkColor;
            if (emotes.Any())
                b.WithDescription($"**Added {emotes.Count} Emotes to {role.Mention}**\n{string.Join("\n", emotes)}");
            if (errored.Any()) b.AddField($"{errored.Count} Errored Emotes", string.Join("\n\n", errored));
            await msg.ModifyAsync(x => { x.Embed = b.Build(); });
        }
    }
}