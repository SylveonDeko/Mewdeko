using System.Threading.Tasks;

namespace Mewdeko.Modules.Server_Management.Services;

public class RoleCommandsService : INService
{
    public readonly List<RoleJobs> Jobslist = new();

    public Task AddToList(IGuild guild, IGuildUser user, int jobId, int totalUsers, string jobType, IRole role,
        IRole? role2 = null)
    {
        var add = new RoleJobs
        {
            StartedBy = user,
            GuildId = guild.Id,
            JobId = jobId,
            TotalUsers = totalUsers,
            JobType = jobType,
            AddedTo = 0,
            Role1 = role,
            Role2 = role2,
            StoppedOrNot = "Running"
        };
        Jobslist.Add(add);
        return Task.CompletedTask;
    }

    public Task UpdateCount(IGuild guild, int jobId, int addedTo)
    {
        var list1 = Jobslist.Find(x => x.GuildId == guild.Id && x.JobId == jobId);
        var add = new RoleJobs
        {
            StartedBy = list1.StartedBy,
            GuildId = list1.GuildId,
            JobId = list1.JobId,
            AddedTo = addedTo,
            TotalUsers = list1.TotalUsers,
            JobType = list1.JobType,
            Role1 = list1.Role1,
            Role2 = list1.Role2,
            StoppedOrNot = list1.StoppedOrNot
        };
        Jobslist.Remove(list1);
        Jobslist.Add(add);
        return Task.CompletedTask;
    }

    public IEnumerable<RoleJobs> JobCheck(IGuild guild, int job) => Jobslist.Where(x => x.GuildId == guild.Id && x.JobId == job).ToArray();

    public async Task StopJob(ITextChannel ch, int jobId, IGuild guild)
    {
        var list1 = Jobslist.Find(x => x.GuildId == guild.Id && x.JobId == jobId);
        var add = new RoleJobs
        {
            StartedBy = list1.StartedBy,
            GuildId = list1.GuildId,
            JobId = list1.JobId,
            AddedTo = list1.AddedTo,
            TotalUsers = list1.TotalUsers,
            JobType = list1.JobType,
            Role1 = list1.Role1,
            Role2 = list1.Role2,
            StoppedOrNot = "Stopped"
        };
        Jobslist.Remove(list1);
        Jobslist.Add(add);
        var eb = new EmbedBuilder
        {
            Description =
                $"Stopping Job {jobId}\nTask: {Format.Bold(list1.JobType)}\nProgress: {list1.AddedTo}/{list1.TotalUsers}\nStarted By: {list1.StartedBy.Mention}",
            Color = Mewdeko.ErrorColor
        };
        await ch.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    public Task RemoveJob(IGuild guild, int job)
    {
        var list = Jobslist.Find(x => x.GuildId == guild.Id && x.JobId == job);
        Jobslist.Remove(list);
        return Task.CompletedTask;
    }

    public class RoleJobs
    {
        public IGuildUser StartedBy { get; set; }
        public ulong GuildId { get; set; }
        public int JobId { get; set; }
        public int AddedTo { get; set; }
        public int TotalUsers { get; set; }
        public string JobType { get; set; }
        public IRole? Role1 { get; set; }
        public IRole? Role2 { get; set; }
        public string StoppedOrNot { get; set; }
    }
}