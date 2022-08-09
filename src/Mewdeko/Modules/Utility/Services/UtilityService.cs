using Mewdeko.Modules.Utility.Common;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VirusTotalNet;
using VirusTotalNet.Results;

namespace Mewdeko.Modules.Utility.Services;

public class UtilityService : INService
{
    private readonly DbService _db;
    private readonly IDataCache _cache;
    private readonly GuildSettingsService _guildSettings;
    private readonly DiscordSocketClient _client;

    public UtilityService(
        DbService db,
        IDataCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        DiscordSocketClient client)
    {
        eventHandler.MessageDeleted += MsgStore;
        eventHandler.MessageUpdated += MsgStore2;
        eventHandler.MessageReceived += MsgReciev;
        eventHandler.MessageReceived += MsgReciev2;
        eventHandler.MessagesBulkDeleted += BulkMsgStore;
        _db = db;
        _cache = cache;
        _guildSettings = guildSettings;
        _client = client;
    }

    public async Task<List<SnipeStore>> GetSnipes(ulong guildId) => await _cache.GetSnipesForGuild(guildId).ConfigureAwait(false);

    public async Task<int> GetPLinks(ulong id) => (await _guildSettings.GetGuildConfig(id)).PreviewLinks;

    public async Task<ulong> GetReactChans(ulong id) => (await _guildSettings.GetGuildConfig(id)).ReactChannel;

    public async Task SetReactChan(IGuild guild, ulong yesnt)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ReactChannel = yesnt;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task PreviewLinks(IGuild guild, string yesnt)
    {
        var yesno = -1;
        await using (_db.GetDbContext().ConfigureAwait(false))
        {
            yesno = yesnt switch
            {
                "y" => 1,
                "n" => 0,
                _ => yesno
            };
        }

        var uow = _db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var gc = await uow.ForGuildId(guild.Id, set => set);
            gc.PreviewLinks = yesno;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            _guildSettings.UpdateGuildConfig(guild.Id, gc);
        }
    }

    public async Task<bool> GetSnipeSet(ulong id) => (await _guildSettings.GetGuildConfig(id)).snipeset;

    public async Task SnipeSet(IGuild guild, string endis)
    {
        var yesno = endis == "enable";
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.snipeset = yesno;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SnipeSetBool(IGuild guild, bool enabled)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.snipeset = enabled;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    private async Task BulkMsgStore(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, Cacheable<IMessageChannel, ulong> channel)
    {
        if (!channel.HasValue)
            return;

        if (channel.Value is not SocketTextChannel chan)
            return;

        if (!await GetSnipeSet(chan.Guild.Id))
            return;

        if (!messages.Select(x => x.HasValue).Any())
            return;

        var msgs = messages.Where(x => x.HasValue).Select(x => new SnipeStore
        {
            GuildId = chan.Guild.Id,
            ChannelId = chan.Id,
            Message = x.Value.Content,
            UserId = x.Value.Author.Id,
            Edited = 0,
            DateAdded = DateTime.UtcNow
        });
        var snipes = await _cache.GetSnipesForGuild(chan.Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
        if (snipes.Count == 0)
        {
            var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
            if (todelete.Any())
                snipes.RemoveRange(todelete);
        }

        snipes.AddRange(msgs);
        await _cache.AddSnipeToCache(chan.Guild.Id, snipes).ConfigureAwait(false);
    }

    private async Task MsgStore(Cacheable<IMessage, ulong> optMsg, Cacheable<IMessageChannel, ulong> ch)
    {
        if (!await GetSnipeSet(((SocketTextChannel)ch.Value).Guild.Id)) return;

        if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg || msg.Author.IsBot) return;
        var user = await msg.Channel.GetUserAsync(optMsg.Value.Author.Id).ConfigureAwait(false);
        if (user is null) return;
        if (!user.IsBot)
        {
            var snipemsg = new SnipeStore
            {
                GuildId = ((SocketTextChannel)ch.Value).Guild.Id,
                ChannelId = ch.Id,
                Message = msg.Content,
                UserId = msg.Author.Id,
                Edited = 0,
                DateAdded = DateTime.UtcNow
            };
            var snipes = await _cache.GetSnipesForGuild(((SocketTextChannel)ch.Value).Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
            if (snipes.Count == 0)
            {
                var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
                if (todelete.Any())
                    snipes.RemoveRange(todelete);
            }

            snipes.Add(snipemsg);
            await _cache.AddSnipeToCache(((SocketTextChannel)ch.Value).Guild.Id, snipes).ConfigureAwait(false);
        }
    }

    private async Task MsgStore2(Cacheable<IMessage, ulong> optMsg, SocketMessage imsg2, ISocketMessageChannel ch)
    {
        if (ch is not ITextChannel)
            return;

        if (!await GetSnipeSet(((SocketTextChannel)ch).Guild.Id)) return;

        if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg || msg.Author.IsBot) return;
        var user = await msg.Channel.GetUserAsync(msg.Author.Id).ConfigureAwait(false);
        if (user is null) return;
        if (!user.IsBot)
        {
            var snipemsg = new SnipeStore
            {
                GuildId = ((SocketTextChannel)ch).Guild.Id,
                ChannelId = ch.Id,
                Message = msg.Content,
                UserId = msg.Author.Id,
                Edited = 1,
                DateAdded = DateTime.UtcNow
            };
            var snipes = await _cache.GetSnipesForGuild(((SocketTextChannel)ch).Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
            if (snipes.Count == 0)
            {
                var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
                if (todelete.Any())
                    snipes.RemoveRange(todelete);
            }

            snipes.Add(snipemsg);
            await _cache.AddSnipeToCache(((SocketTextChannel)ch).Guild.Id, snipes).ConfigureAwait(false);
        }
    }

    public async Task MsgReciev2(IMessage msg)
    {
        if (msg.Author.IsBot) return;
        if (msg.Channel is SocketDMChannel) return;
        var guild = ((SocketGuildChannel)msg.Channel).Guild.Id;
        var id = await GetReactChans(guild);
        if (msg.Channel.Id == id)
        {
            Emote.TryParse("<:upvote:863122283283742791>", out var emote);
            Emote.TryParse("<:D_downvote:863122244527980613>", out var emote2);
            await Task.Delay(200).ConfigureAwait(false);
            await msg.AddReactionAsync(emote).ConfigureAwait(false);
            await Task.Delay(200).ConfigureAwait(false);
            await msg.AddReactionAsync(emote2).ConfigureAwait(false);
        }
    }

    public static async Task<UrlReport> UrlChecker(string url)
    {
        var vcheck = new VirusTotal("e49046afa41fdf4e8ca72ea58a5542d0b8fbf72189d54726eed300d2afe5d9a9");
        return await vcheck.GetUrlReportAsync(url, true).ConfigureAwait(false);
    }

    public async Task MsgReciev(IMessage msg)
    {
        if (msg.Channel is SocketTextChannel t)
        {
            if (msg.Author.IsBot) return;
            var gid = t.Guild;
            if (await GetPLinks(gid.Id) == 1)
            {
                var linkParser = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                foreach (Match m in linkParser.Matches(msg.Content))
                {
                    var e = new Uri(m.Value);
                    var en = e.Host.Split(".");
                    if (!en.Contains("discord")) continue;
                    var eb = string.Join("", e.Segments).Split("/");
                    if (!eb.Contains("channels")) continue;
                    SocketGuild guild;
                    if (gid.Id != Convert.ToUInt64(eb[2]))
                    {
                        guild = _client.GetGuild(Convert.ToUInt64(eb[2]));
                        if (guild is null) return;
                    }
                    else
                    {
                        guild = gid;
                    }

                    if (guild != t.Guild)
                        return;
                    var em = await ((IGuild)guild).GetTextChannelAsync(Convert.ToUInt64(eb[3])).ConfigureAwait(false);
                    if (em == null) return;
                    var msg2 = await em.GetMessageAsync(Convert.ToUInt64(eb[4])).ConfigureAwait(false);
                    if (msg2 is null) return;
                    var en2 = new EmbedBuilder
                    {
                        Color = Mewdeko.OkColor,
                        Author = new EmbedAuthorBuilder { Name = msg2.Author.Username, IconUrl = msg2.Author.GetAvatarUrl(size: 2048) },
                        Footer = new EmbedFooterBuilder { IconUrl = ((IGuild)guild).IconUrl, Text = $"{((IGuild)guild).Name}: {em.Name}" }
                    };
                    if (msg2.Embeds.Count > 0)
                    {
                        en2.AddField("Embed Content:", msg2.Embeds.FirstOrDefault()?.Description);
                        if (msg2.Embeds.FirstOrDefault()!.Image != null)
                        {
                            var embedImage = msg2.Embeds.FirstOrDefault()?.Image;
                            if (embedImage != null)
                                en2.ImageUrl = embedImage?.Url;
                        }
                    }

                    if (msg2.Content.Length > 0) en2.Description = msg2.Content;

                    if (msg2.Attachments.Count > 0) en2.ImageUrl = msg2.Attachments.FirstOrDefault().Url;

                    await msg.Channel.SendMessageAsync(embed: en2.WithTimestamp(msg2.Timestamp).Build()).ConfigureAwait(false);
                }
            }
        }
    }
}