using LinqToDB.Reflection;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Threading.Tasks;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Karuta.Services;

public class ShibaKarutaService : INService
{
    private readonly ConcurrentDictionary<ulong, int> _messagesSent = new();
    private readonly ConcurrentDictionary<ulong, int> _messagesSent1 = new();
    private readonly DbService _db;
    private readonly GuildSettingsService _guildSettingsService;
    private readonly HttpClient _httpClient;
    private readonly DiscordSocketClient _client;
    public ShibaKarutaService(EventHandler handler, DbService db, GuildSettingsService guildSettingsService,
        HttpClient httpClient,
        DiscordSocketClient client)
    {
        _db = db;
        _guildSettingsService = guildSettingsService;
        _httpClient = httpClient;
        _client = client;
        handler.MessageReceived += GrantKarutaRole;
        handler.MessageReceived += GrantKarutaRole1;
        handler.MessageReceived += RepostKarutaImage;
    }

    private async Task RepostKarutaImage(SocketMessage args)
    {
        if (args.Channel is IDMChannel)
            return;

        var channel = args.Channel as ITextChannel;
        var eventChannel = await GetEventChannel(channel.GuildId);
        if (channel.Id != eventChannel)
            return;

        if (!args.Attachments.Any()) return;
        using var sr = await _httpClient.GetAsync(args.Attachments.First().Url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await using var uow = _db.GetDbContext();
        await args.DeleteAsync();
        var entries = uow.KarutaEventEntry.AsQueryable().Where(x => x.GuildId == channel.GuildId).ToList()/*I fucking hate SQLite.*/.LastOrDefault();
        var entrynum = 1;
        if (entries is not null)
        {
            entrynum = entries.EntryNumber + 1;
            var componentBuilder = new ComponentBuilder();
            for (var i = 1; i < 7; i++)
            {
                var text = await GetButtonText(channel.GuildId, i);
                if (string.IsNullOrEmpty(text))
                    continue;
                componentBuilder.WithButton(text, $"karutaeventbutton:{i}");
            }

            var msg = await channel.SendFileAsync(imgStream, filename: args.Attachments.First().Filename,
                embed: new EmbedBuilder().WithOkColor().WithTitle($"Entry #{entrynum}").WithImageUrl($"attachment://{args.Attachments.First().Filename}").Build(),
                components: componentBuilder.Build());                           
            var toadd = new KarutaEventEntry
            {
                ChannelId = eventChannel,
                GuildId = channel.GuildId,
                EntryNumber = entrynum,
                MessageId = msg.Id
            };
            await uow.KarutaEventEntry.AddAsync(toadd);
            await uow.SaveChangesAsync();
        }
        else
        {
            var componentBuilder = new ComponentBuilder();
            for (var i = 1; i < 7; i++)
            {
                var text = await GetButtonText(channel.GuildId, i);
                if (string.IsNullOrEmpty(text))
                    continue;
                componentBuilder.WithButton(text, $"karutaeventbutton:{i}");
            }

            var msg = await channel.SendFileAsync(imgStream, filename: args.Attachments.First().Filename,
                embed: new EmbedBuilder().WithOkColor().WithTitle($"Entry #{entrynum}").WithImageUrl($"attachment://{args.Attachments.First().Filename}").Build(),
                components: componentBuilder?.Build());                           
            var toadd = new KarutaEventEntry
            {
                ChannelId = eventChannel,
                GuildId = channel.GuildId,
                EntryNumber = entrynum,
                MessageId = msg.Id
            };
            await uow.KarutaEventEntry.AddAsync(toadd);
            await uow.SaveChangesAsync();
        }
    }

    private async Task GrantKarutaRole(IMessage arg)

    {
        if (arg.Channel is not ITextChannel channel)

            return;

        if (channel.Id != 940654772070019132 && channel.Id != 809636962599829574)

            return;

        var gUser = arg.Author as SocketGuildUser;

        if (gUser.Roles.Select(x => x.Id).Contains<ulong>(940669747282980954))

            return;

        if (!_messagesSent.TryGetValue(gUser.Id, out var amount) || amount < 2)

            _messagesSent.AddOrUpdate(gUser.Id, amount++, (_, _) => amount++);

        else

        {

            await gUser.AddRoleAsync(940669747282980954);

            _messagesSent.TryRemove(gUser.Id, out _);

        }

    }

    private async Task GrantKarutaRole1(IMessage arg)

    {

        if (arg.Channel is not ITextChannel channel)
            return;

        if (channel.Id is not 952697336570728498 or 954828857985351740 or 952698660179808297)
            return;

        var gUser = arg.Author as SocketGuildUser;

        if (gUser.Roles.Select(x => x.Id).Contains<ulong>(952773926730203146))
            return;

        if (!_messagesSent1.TryGetValue(gUser.Id, out var amount) || amount < 3)

            _messagesSent1.AddOrUpdate(gUser.Id, amount++, (_, _) => amount++);

        else

        {

            await gUser.AddRoleAsync(952773926730203146);

            _messagesSent1.TryRemove(gUser.Id, out _);

        }
    }

    public async Task SetEventChannel(ulong guildId, ulong channelId)
    {
        await using var uow = _db.GetDbContext();

        var gc = await uow.ForGuildId(guildId, set => set);
        gc.KarutaEventChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettingsService.UpdateGuildConfig(guildId, gc);
    }

    public async Task<ulong> GetEventChannel(ulong guildId) 
        => (await _guildSettingsService.GetGuildConfig(guildId)).KarutaEventChannel;

    public async Task SetButtonText(ulong guildId, int buttonNumber, string text)
    {
        await using var uow = _db.GetDbContext();
        var get = await uow.KarutaButtonOptions.FirstOrDefaultAsync(x => x.GuildId == guildId);
        var toadd = get ?? new KarutaButtonOptions { GuildId = guildId };

        switch (buttonNumber)
        {
            case 1:
                toadd.Button1Text = text;
                break;
            case 2:
                toadd.Button2Text = text;
                break;
            case 3:
                toadd.Button3Text = text;
                break;
            case 4:
                toadd.Button4Text = text;
                break;
            case 5:
                toadd.Button5Text = text;
                break;
            case 6:
                toadd.Button6Text = text;
                break;
            
        }
        
        uow.KarutaButtonOptions.Update(toadd);
        await uow.SaveChangesAsync();
    }

    public async Task ClearEntries(ulong guildId)
    {
        await using var uow = _db.GetDbContext();
        var entries = uow.KarutaEventEntry.Where(x => x.GuildId == guildId);
        var votes = uow.KarutaEventVotes.Where(x => x.GuildId == guildId);
        if (entries.Any())
            uow.KarutaEventEntry.RemoveRange(entries);
        if (votes.Any())
            uow.KarutaEventVotes.RemoveRange(votes);
        await uow.SaveChangesAsync();

    }

    public async Task<bool> RemoveEntry(ulong guildId, int entryNum)
    {
        await using var uow = _db.GetDbContext();
        var entry = uow.KarutaEventEntry.FirstOrDefault(x => x.GuildId == guildId && x.EntryNumber == entryNum);
        if (entry is null)
            return false;
        var votes = uow.KarutaEventVotes.Where(x => x.GuildId == guildId && x.MessageId == entry.MessageId);
        uow.KarutaEventEntry.Remove(entry);
        if (votes.Any())
            uow.KarutaEventVotes.RemoveRange(votes);
        await uow.SaveChangesAsync();
        var channelId = await GetEventChannel(guildId);
        var channel = _client.GetChannel(channelId) as SocketTextChannel;
        var message = await channel.GetMessageAsync(entry.MessageId);
        try
        {
            await message.DeleteAsync();
        }
        catch
        {
           // ignored
        }
        return true;
    }

    public async Task<string> GetButtonText(ulong guildId, int button)
    {
        await using var uow = _db.GetDbContext();
        var get = await uow.KarutaButtonOptions.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (get is null)
            return string.Empty;

        return button switch
        {
            1 => get.Button1Text,
            2 => get.Button2Text,
            3 => get.Button3Text,
            4 => get.Button4Text,
            5 => get.Button5Text,
            6 => get.Button6Text,
            _ => string.Empty
        };
    }

    public async Task<Embed?> GetLeaderboardEmbed(ulong guildId, int voteNum)
    {
        await using var uow = _db.GetDbContext();
        var buttonText = await GetButtonText(guildId, voteNum);
        IEnumerable<KarutaEventEntry> entries;
        if (string.IsNullOrEmpty(buttonText))
            return null;
        switch (voteNum)
        {
            case 1:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button1Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button1Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            case 2:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button2Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button2Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            case 3:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button3Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button3Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            case 4:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button4Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button4Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            case 5:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button5Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button5Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            case 6:
                entries = uow.KarutaEventEntry.ToList().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Button6Count).Take(8);
                return new EmbedBuilder().WithOkColor().WithTitle($"Top 8 for {buttonText}").WithDescription(entries.Any()
                    ? string.Join("\n", entries.Select(x => $"**[Entry {x.EntryNumber} - {x.Button6Count}]({GetJumpUrl(x.GuildId, x.ChannelId, x.MessageId)})**"))
                    : "**No recorded entries yet.**").Build();
            default:
                return null;
        }
        
    }
        
    public static string GetJumpUrl(ulong guildId, ulong channelId, ulong messageId)
        => $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";
}