using System.Threading.Tasks;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Games.Common;
using Serilog;

namespace Mewdeko.Modules.Games.Services;

public class PollService : INService
{
    private readonly DbService db;

    public PollService(DbService db)
    {
        this.db = db;

        using var uow = db.GetDbContext();
        ActivePolls = uow.Poll.GetAllPolls()
            .ToDictionary(x => x.GuildId, x => new PollRunner(db, x))
            .ToConcurrent();
    }

    public ConcurrentDictionary<ulong, PollRunner> ActivePolls { get; }

    public async Task<(bool allowed, PollType type)> TryVote(IGuild guild, int num, IUser user)
    {
        if (!ActivePolls.TryGetValue(guild.Id, out var poll))
            return (false, PollType.PollEnded);

        try
        {
            return await poll.TryVote(num, user).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error voting");
        }

        return (true, poll.Poll.PollType);
    }

    public static Poll? CreatePoll(ulong guildId, ulong channelId, string input, PollType type)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.Contains(';'))
            return null;
        var data = input.Split(';');
        if (data.Length < 3)
            return null;

        var col = new IndexedCollection<PollAnswer>(data.Skip(1)
            .Select(x => new PollAnswer
            {
                Text = x
            }));

        return new Poll
        {
            Answers = col,
            Question = data[0],
            ChannelId = channelId,
            GuildId = guildId,
            Votes = new List<PollVote>(),
            PollType = type
        };
    }

    public bool StartPoll(Poll p)
    {
        var pr = new PollRunner(db, p);
        if (!ActivePolls.TryAdd(p.GuildId, pr)) return false;
        using var uow = db.GetDbContext();
        uow.Poll.Add(p);
        uow.SaveChanges();
        return true;
    }

    public async Task<Poll?> StopPoll(ulong guildId)
    {
        if (!ActivePolls.TryRemove(guildId, out var pr)) return null;
        await using (var uow = db.GetDbContext())
        {
            await uow.RemovePoll(pr.Poll.Id);
            await uow.SaveChangesAsync();
        }

        return pr.Poll;
    }
}