namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     A service responsible for managing role assignment and removal jobs within a guild.
/// </summary>
public class RoleCommandsService : INService
{
    /// <summary>
    ///     A list maintaining active and historical role job records.
    /// </summary>
    public readonly List<RoleJobs> Jobslist = [];

    /// <summary>
    ///     Adds a role job to the list with the specified parameters.
    /// </summary>
    /// <param name="guild">The guild where the job is initiated.</param>
    /// <param name="user">The user who initiated the job.</param>
    /// <param name="jobId">The unique identifier for the job.</param>
    /// <param name="totalUsers">The total number of users affected by the job.</param>
    /// <param name="jobType">The type of job (e.g., role addition or removal).</param>
    /// <param name="role">The primary role involved in the job.</param>
    /// <param name="role2">An optional secondary role involved in the job.</param>
    /// <returns>A task that represents the asynchronous operation of adding the job to the list.</returns>
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

    /// <summary>
    ///     Updates the progress count of a specific job.
    /// </summary>
    /// <param name="guild">The guild where the job is being executed.</param>
    /// <param name="jobId">The identifier of the job to update.</param>
    /// <param name="addedTo">The new progress count of users processed by the job.</param>
    /// <returns>A task that represents the asynchronous operation of updating the job's progress.</returns>
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

    /// <summary>
    ///     Checks the details of a specific job by its identifier.
    /// </summary>
    /// <param name="guild">The guild where the job is being executed.</param>
    /// <param name="job">The identifier of the job to check.</param>
    /// <returns>An enumerable of <see cref="RoleJobs" /> that matches the specified job identifier.</returns>
    public IEnumerable<RoleJobs> JobCheck(IGuild guild, int job)
    {
        return Jobslist.Where(x => x.GuildId == guild.Id && x.JobId == job).ToArray();
    }

    /// <summary>
    ///     Stops a specified job and notifies the channel about the stoppage.
    /// </summary>
    /// <param name="ch">The text channel to send the stop notification.</param>
    /// <param name="jobId">The identifier of the job to stop.</param>
    /// <param name="guild">The guild where the job is being executed.</param>
    /// <returns>A task that represents the asynchronous operation of stopping the job and notifying the channel.</returns>
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

    /// <summary>
    ///     Removes a specified job from the jobs list.
    /// </summary>
    /// <param name="guild">The guild where the job is being executed.</param>
    /// <param name="job">The identifier of the job to remove.</param>
    /// <returns>A task that represents the asynchronous operation of removing the job from the list.</returns>
    public Task RemoveJob(IGuild guild, int job)
    {
        var list = Jobslist.Find(x => x.GuildId == guild.Id && x.JobId == job);
        Jobslist.Remove(list);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Represents a job related to role assignments or removals in a Discord guild.
    /// </summary>
    public class RoleJobs
    {
        /// <summary>
        ///     The user who initiated the role job.
        /// </summary>
        public IGuildUser StartedBy { get; set; }

        /// <summary>
        ///     The unique identifier of the guild where the role job is being executed.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     A unique identifier for the role job, typically used for tracking and management purposes.
        /// </summary>
        public int JobId { get; set; }

        /// <summary>
        ///     The number of users that have already been processed by this role job.
        /// </summary>
        public int AddedTo { get; set; }

        /// <summary>
        ///     The total number of users targeted by this role job.
        /// </summary>
        public int TotalUsers { get; set; }

        /// <summary>
        ///     Describes the type of job being performed, such as "role addition" or "role removal."
        /// </summary>
        public string JobType { get; set; }

        /// <summary>
        ///     The primary role involved in the job, which may be assigned or removed from users.
        /// </summary>
        public IRole? Role1 { get; set; }

        /// <summary>
        ///     An optional secondary role that may also be involved in the job.
        /// </summary>
        public IRole? Role2 { get; set; }

        /// <summary>
        ///     Indicates whether the job is currently running or has been stopped.
        /// </summary>
        public string StoppedOrNot { get; set; }
    }
}