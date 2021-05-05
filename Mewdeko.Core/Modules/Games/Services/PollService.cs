using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Core.Services;
using NLog;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Common.Collections;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Games.Services
{
    public class PollService : IEarlyBehavior, INService
    {
        public ConcurrentDictionary<ulong, PollRunner> ActivePolls { get; } = new ConcurrentDictionary<ulong, PollRunner>();

        public int Priority => -5;
        public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly IBotStrings _strings;
        private readonly DbService _db;
        private readonly IBotStrings _strs;

        public PollService(DiscordSocketClient client, IBotStrings strings, DbService db,
            IBotStrings strs)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _strings = strings;
            _db = db;
            _strs = strs;

            using (var uow = db.GetDbContext())
            {
                ActivePolls = uow.Polls.GetAllPolls()
                    .ToDictionary(x => x.GuildId, x =>
                    {
                        var pr = new PollRunner(db, x);
                        pr.OnVoted += Pr_OnVoted;
                        return pr;
                    })
                    .ToConcurrent();
            }
        }

        public Poll CreatePoll(ulong guildId, ulong channelId, string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(";"))
                return null;
            var data = input.Split(';');
            if (data.Length < 3)
                return null;

            var col = new IndexedCollection<PollAnswer>(data.Skip(1)
                .Select(x => new PollAnswer() { Text = x }));

            return new Poll()
            {
                Answers = col,
                Question = data[0],
                ChannelId = channelId,
                GuildId = guildId,
                Votes = new System.Collections.Generic.HashSet<PollVote>()
            };
        }

        public bool StartPoll(Poll p)
        {
            var pr = new PollRunner(_db,  p);
            if (ActivePolls.TryAdd(p.GuildId, pr))
            {
                using (var uow = _db.GetDbContext())
                {
                    uow.Polls.Add(p);
                    uow.SaveChanges();
                }

                pr.OnVoted += Pr_OnVoted;
                return true;
            }
            return false;
        }

        public Poll StopPoll(ulong guildId)
        {
            if (ActivePolls.TryRemove(guildId, out var pr))
            {
                pr.OnVoted -= Pr_OnVoted;
                using (var uow = _db.GetDbContext())
                {
                    uow.Polls.RemovePoll(pr.Poll.Id);
                    uow.SaveChanges();
                }
                return pr.Poll;
            }
            return null;
        }

        private async Task Pr_OnVoted(IUserMessage msg, IGuildUser usr)
        {
            var toDelete = await msg.Channel.SendConfirmAsync(_strs.GetText("poll_voted", 
                    usr.Guild.Id, Format.Bold(usr.ToString())))
                .ConfigureAwait(false);
            toDelete.DeleteAfter(5);
            try { await msg.DeleteAsync().ConfigureAwait(false); } catch { }
        }

        public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            if (guild == null)
                return false;

            if (!ActivePolls.TryGetValue(guild.Id, out var poll))
                return false;

            try
            {
                return await poll.TryVote(msg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
            }

            return false;
        }
    }
}
