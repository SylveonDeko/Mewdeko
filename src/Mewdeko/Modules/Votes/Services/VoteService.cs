using System.Threading.Tasks;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Votes.Common;

namespace Mewdeko.Modules.Votes.Services;

public class VoteService : INService
{
    private readonly DbService db;
    private readonly DiscordSocketClient client;
    private readonly MuteService muteService;

    public VoteService(IPubSub pubSub, DbService db, DiscordSocketClient client,
        MuteService muteService)
    {
        this.db = db;
        this.client = client;
        this.muteService = muteService;
        var typedKey = new TypedKey<CompoundVoteModal>("uservoted");
        pubSub.Sub(typedKey, RunVoteStuff);
    }

    private async ValueTask RunVoteStuff(CompoundVoteModal voteModal)
    {
        await using var uow = db.GetDbContext();
        var potentialVoteConfig = await uow.GuildConfigs.FirstOrDefaultAsyncEF(x => x.VotesPassword == voteModal.Password);
        if (potentialVoteConfig is null)
            return;
        var guild = client.GetGuild(potentialVoteConfig.GuildId);
        if (guild is null)
            return;
        var user = guild.GetUser(ulong.Parse(voteModal.VoteModel.User));
        var newVote = new Database.Models.Votes
        {
            UserId = user.Id, GuildId = guild.Id
        };
        await uow.Votes.AddAsync(newVote);
        await uow.SaveChangesAsync();
        if (string.IsNullOrEmpty(potentialVoteConfig.VoteEmbed))
        {
            if (potentialVoteConfig.VotesChannel is 0)
                return;

            if (guild.GetTextChannel(potentialVoteConfig.VotesChannel) is not ITextChannel channel)
                return;

            var votes = await uow.Votes.CountAsyncEF(x => x.UserId == user.Id && x.GuildId == guild.Id);
            var eb = new EmbedBuilder()
                .WithTitle($"Thanks for voting for {guild.Name}")
                .WithDescription($"You have voted a total of {votes} times!")
                .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
                .WithOkColor();

            await channel.SendMessageAsync(user.Mention, embed: eb.Build());

            if (!await uow.VoteRoles.AnyAsyncEF(x => x.GuildId == guild.Id))
                return;
            var split = uow.VoteRoles.Where(x => x.GuildId == guild.Id);
            if (!split.Any())
                return;
            foreach (var i in split)
            {
                var role = guild.GetRole(i.RoleId);
                try
                {
                    await user.AddRoleAsync(role);
                }
                catch
                {
                    // ignored, means role was too high or inaccessible
                }

                if (i.Timer is 0) continue;
                var timespan = TimeSpan.FromSeconds(i.Timer);
                await muteService.TimedRole(user, timespan, "Vote Role", role);
            }
        }
        else
        {
            if (guild.GetTextChannel(potentialVoteConfig.VotesChannel) is not ITextChannel channel)
                return;
            if (potentialVoteConfig.VotesChannel is 0)
                return;
            var votes = uow.Votes.Where(x => x.UserId == user.Id && x.GuildId == guild.Id);
            var rep = new ReplacementBuilder()
                .WithDefault(user, null, guild, client)
                .WithOverride("%votestotalcount%", () => votes.Count().ToString())
                .WithOverride("%votesmonthcount%", () => votes.Count(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month).ToString()).Build();

            if (SmartEmbed.TryParse(rep.Replace(potentialVoteConfig.VoteEmbed), guild.Id, out var embeds, out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
            }
            else
            {
                await channel.SendMessageAsync(rep.Replace(potentialVoteConfig.VoteEmbed).SanitizeMentions());
            }

            if (!await uow.VoteRoles.AnyAsyncEF(x => x.GuildId == guild.Id))
                return;
            var split = uow.VoteRoles.Where(x => x.GuildId == guild.Id);
            if (!split.Any())
                return;
            foreach (var i in split)
            {
                var role = guild.GetRole(i.RoleId);
                try
                {
                    await user.AddRoleAsync(role);
                }
                catch
                {
                    // ignored, means role was too high or inaccessible
                }

                if (i.Timer is 0) continue;
                var timespan = TimeSpan.FromSeconds(i.Timer);
                await muteService.TimedRole(user, timespan, "Vote Role", role);
            }
        }
    }

    public async Task<(bool, string)> AddVoteRole(ulong guildId, ulong roleId, int seconds = 0)
    {
        if (roleId == guildId)
            return (false, "Unable to add the everyone role you dumdum");
        await using var uow = db.GetDbContext();
        if (await uow.VoteRoles.CountAsyncEF(x => x.GuildId == guildId) >= 10)
            return (false, "Reached maximum of 10 VoteRoles");

        if (await uow.VoteRoles.Select(x => x.RoleId).ContainsAsyncEF(roleId))
            return (false, "Role is already set as a VoteRole.");

        var voteRole = new VoteRoles
        {
            RoleId = roleId, GuildId = guildId, Timer = seconds
        };

        await uow.VoteRoles.AddAsync(voteRole);
        await uow.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool, string)> RemoveVoteRole(ulong guildId, ulong roleId)
    {
        if (roleId == guildId)
            return (false, "Unable to add the everyone role you dumdum");
        await using var uow = db.GetDbContext();
        if (!await uow.VoteRoles.AnyAsyncEF(x => x.GuildId == guildId))
            return (false, "You don't have any VoteRoles");
        var voteRole = await uow.VoteRoles.FirstOrDefaultAsyncEF(x => x.RoleId == roleId);
        if (voteRole is null)
            return (false, "Role is not a VoteRole.");

        uow.VoteRoles.Remove(voteRole);
        await uow.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool, string)> UpdateTimer(ulong roleId, int seconds)
    {
        await using var uow = db.GetDbContext();
        var voteRole = await uow.VoteRoles.FirstOrDefaultAsyncEF(x => x.RoleId == roleId);
        if (voteRole is null)
            return (false, "Role to update is not added as a VoteRole.");
        if (voteRole.Timer == seconds)
            return (false, "VoteRole timer is already set to that value.");
        voteRole.Timer = seconds;
        uow.VoteRoles.Update(voteRole);
        await uow.SaveChangesAsync();
        return (true, null);
    }

    public async Task<IList<VoteRoles>> GetVoteRoles(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        return await uow.VoteRoles.Where(x => x.GuildId == guildId)?.ToListAsyncEF() ?? new List<VoteRoles>();
    }

    public async Task<string> GetVoteMessage(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId);
        return gc.VoteEmbed;
    }

    public async Task<(bool, string)> ClearVoteRoles(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var voteRoles = uow.VoteRoles.Where(x => x.GuildId == guildId);
        if (!await voteRoles.AnyAsyncEF())
            return (false, "There are no VoteRoles set.");
        uow.VoteRoles.RemoveRange(voteRoles);
        await uow.SaveChangesAsync();
        return (true, null);
    }

    public async Task SetVoteMessage(ulong guildId, string message)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);
        config.VoteEmbed = message;
        await uow.SaveChangesAsync();
    }

    public async Task SetVotePassword(ulong guildId, string password)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);
        config.VotesPassword = password;
        await uow.SaveChangesAsync();
    }

    public async Task SetVoteChannel(ulong guildId, ulong channel)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);
        config.VotesChannel = channel;
        await uow.SaveChangesAsync();
    }

    public async Task<List<Database.Models.Votes>> GetVotes(ulong guildId, ulong userId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Votes.Where(x => x.GuildId == guildId && x.UserId == userId).ToListAsyncEF();
    }

    public async Task<List<Database.Models.Votes>> GetVotes(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Votes.Where(x => x.GuildId == guildId).ToListAsyncEF();
    }

    public async Task<EmbedBuilder> GetTotalVotes(IUser user, IGuild guild)
    {
        await using var uow = db.GetDbContext();
        var thisMonth = await uow.Votes.CountAsyncEF(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month && x.UserId == user.Id && x.GuildId == guild.Id);
        var total = await uow.Votes.CountAsyncEF(x => x.GuildId == guild.Id && x.UserId == user.Id);
        if (total is 0)
            return new EmbedBuilder().WithErrorColor().WithDescription("You do not have any votes.");
        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"Vote Stats for {guild.Name}")
            .AddField("Votes this month", thisMonth)
            .AddField("Total Votes", total)
            .WithThumbnailUrl(user.RealAvatarUrl().AbsoluteUri)
            .WithFooter(new EmbedFooterBuilder().WithIconUrl(user.RealAvatarUrl().AbsoluteUri).WithText($"{user} | {user.Id}"));
    }
}

public class CustomVoteThingy
{
    public IUser User { get; set; }
    public int VoteCount { get; set; }
}