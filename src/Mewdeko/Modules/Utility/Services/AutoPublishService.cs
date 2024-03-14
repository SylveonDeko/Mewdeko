using LinqToDB.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

public class AutoPublishService : INService
{
    private readonly DbService dbService;
    private readonly DiscordSocketClient client;

    public AutoPublishService(DbService service, EventHandler handler, DiscordSocketClient client)
    {
        this.client = client;
        dbService = service;
        handler.MessageReceived += AutoPublish;
    }

    private async Task AutoPublish(SocketMessage args)
    {
        if (args.Channel is not INewsChannel channel)
            return;

        if (args is not IUserMessage msg)
            return;

        var curuser = await channel.GetUserAsync(client.CurrentUser.Id);

        var perms = curuser.GetPermissions(channel);

        if (!perms.Has(ChannelPermission.ManageMessages) &&
            curuser.GuildPermissions.Has(GuildPermission.ManageMessages))
            return;


        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstAsyncEF(x => x.ChannelId == channel.Id);
        if (autoPublish is null)
            return;

        var blacklistedWords = uow.PublishWordBlacklists.Where(x => x.ChannelId == channel.Id);
        if (blacklistedWords.Any())
        {
            if (blacklistedWords.Any(i => args.Content.ToLower().Contains(i.Word)))
            {
                return;
            }
        }

        var userBlacklists = uow.PublishUserBlacklists.Where(x => x.ChannelId == channel.Id);

        if (userBlacklists.Any())
        {
            if (userBlacklists.Any(i => args.Author.Id == i.User))
            {
                return;
            }
        }

        try
        {
            await msg.CrosspostAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "Unable to publish message:");
        }
    }


    public async Task<bool> AddAutoPublish(ulong guildId, ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (autoPublish != null)
        {
            // AutoPublish already exists for the channel
            return false;
        }

        autoPublish = new AutoPublish
        {
            GuildId = guildId, ChannelId = channelId
        };
        uow.AutoPublish.Add(autoPublish);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<List<(AutoPublish?, List<PublishUserBlacklist?>, List<PublishWordBlacklist?>)>>
        GetAutoPublishes(ulong guildId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublishes = uow.AutoPublish.Where(x => x.GuildId == guildId).ToHashSet();

        return !autoPublishes.Any()
            ? [(null, null, null)]
            : (from i in autoPublishes
                let userBlacklists = uow.PublishUserBlacklists.Where(x => x.ChannelId == i.ChannelId).ToList()
                let wordBlacklists = uow.PublishWordBlacklists.Where(x => x.ChannelId == i.ChannelId).ToList()
                select (i, userBlacklists, wordBlacklists)).ToList();
    }

    public async Task<bool> PermCheck(INewsChannel channel)
    {
        var curuser = await channel.GetUserAsync(client.CurrentUser.Id);

        var perms = curuser.GetPermissions(channel);

        return perms.Has(ChannelPermission.ManageMessages) ||
               !curuser.GuildPermissions.Has(GuildPermission.ManageMessages);
    }

    public async Task<bool> RemoveAutoPublish(ulong guildId, ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (autoPublish == null)
        {
            return false;
        }

        uow.AutoPublish.Remove(autoPublish);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddUserToBlacklist(ulong channelId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();
        var userBlacklist =
            await uow.PublishUserBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (userBlacklist != null)
        {
            // User is already blacklisted in the channel
            return false;
        }

        userBlacklist = new PublishUserBlacklist
        {
            ChannelId = channelId, User = userId
        };
        uow.PublishUserBlacklists.Add(userBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserFromBlacklist(ulong channelId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();
        var userBlacklist =
            await uow.PublishUserBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (userBlacklist == null)
        {
            // User is not blacklisted in the channel
            return false;
        }

        uow.PublishUserBlacklists.Remove(userBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddWordToBlacklist(ulong channelId, string word)
    {
        await using var uow = dbService.GetDbContext();
        var wordBlacklist =
            await uow.PublishWordBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.Word == word);
        if (wordBlacklist != null)
        {
            // Word is already blacklisted in the channel
            return false;
        }

        wordBlacklist = new PublishWordBlacklist
        {
            ChannelId = channelId, Word = word
        };
        uow.PublishWordBlacklists.Add(wordBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveWordFromBlacklist(ulong channelId, string word)
    {
        await using var uow = dbService.GetDbContext();
        var wordBlacklist =
            await uow.PublishWordBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.Word == word);
        if (wordBlacklist == null)
        {
            // Word is not blacklisted in the channel
            return false;
        }

        uow.PublishWordBlacklists.Remove(wordBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CheckIfExists(ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        return (await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId)) is null;
    }

    public class CustomAutoPublish
    {
        public AutoPublish autoPublish { get; set; }
        public List<PublishWordBlacklist>? wordBlacklist { get; set; }
        public List<PublishUserBlacklist>? userBlacklist { get; set; }
    }
}