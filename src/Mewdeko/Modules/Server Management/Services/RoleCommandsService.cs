using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace Mewdeko.Modules.Server_Management.Services;

public class RoleCommandsService : INService
{
    public List<RoleJobs> jobslist = new();

    public Task AddToList(IGuild guild, IGuildUser user, int JobId, int TotalUsers, string JobType, IRole role,
        IRole role2 = null)
    {
        var add = new RoleJobs
        {
            StartedBy = user,
            GuildId = guild.Id,
            JobId = JobId,
            TotalUsers = TotalUsers,
            JobType = JobType,
            AddedTo = 0,
            Role1 = role,
            Role2 = role2,
            StoppedOrNot = "Running"
        };
        jobslist.Add(add);
        return Task.CompletedTask;
    }

    public Task UpdateCount(IGuild guild, int JobId, int AddedTo)
    {
        var list1 = jobslist.FirstOrDefault(x => x.GuildId == guild.Id && x.JobId == JobId);
        var add = new RoleJobs
        {
            StartedBy = list1.StartedBy,
            GuildId = list1.GuildId,
            JobId = list1.JobId,
            AddedTo = AddedTo,
            TotalUsers = list1.TotalUsers,
            JobType = list1.JobType,
            Role1 = list1.Role1,
            Role2 = list1.Role2,
            StoppedOrNot = list1.StoppedOrNot
        };
        jobslist.Remove(list1);
        jobslist.Add(add);
        return Task.CompletedTask;
    }

    public RoleJobs[] JobCheck(IGuild guild, int job) => jobslist.Where(x => x.GuildId == guild.Id && x.JobId == job).ToArray();

    public async Task StopJob(ITextChannel ch, int jobId, IGuild guild)
    {
        var list1 = jobslist.FirstOrDefault(x => x.GuildId == guild.Id && x.JobId == jobId);
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
        jobslist.Remove(list1);
        jobslist.Add(add);
        var eb = new EmbedBuilder
        {
            Description =
                $"Stopping Job {jobId}\nTask: {Format.Bold(list1.JobType)}\nProgress: {list1.AddedTo}/{list1.TotalUsers}\nStarted By: {list1.StartedBy.Mention}",
            Color = Mewdeko.Services.Mewdeko.ErrorColor
        };
        await ch.SendMessageAsync(embed: eb.Build());
    }

    public Task RemoveJob(IGuild guild, int job)
    {
        var list = jobslist.FirstOrDefault(x => x.GuildId == guild.Id && x.JobId == job);
        jobslist.Remove(list);
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
        public IRole Role1 { get; set; }
        public IRole Role2 { get; set; }
        public string StoppedOrNot { get; set; }
    }
}