using System.Threading;
using Mewdeko.Common.Collections;

namespace Mewdeko.Modules.Administration.Common
{
    /// <summary>
    /// Enumeration representing different types of protection against unwanted activities.
    /// </summary>
    public enum ProtectionType
    {
        /// <summary>
        /// Protection against raiding.
        /// </summary>
        Raiding,

        /// <summary>
        /// Protection against spamming.
        /// </summary>
        Spamming,

        /// <summary>
        /// Protection against alting.
        /// </summary>
        Alting,

        /// <summary>
        /// Protection against mass mention.
        /// </summary>
        MassMention
    }

    /// <summary>
    /// Represents statistics related to anti-raid measures.
    /// </summary>
    public class AntiRaidStats
    {
        /// <summary>
        /// Gets or sets the anti-raid settings.
        /// </summary>
        public AntiRaidSetting AntiRaidSettings { get; set; }

        /// <summary>
        /// Gets or sets the count of users involved in the raid.
        /// </summary>
        public int UsersCount { get; set; }

        /// <summary>
        /// Gets or sets the set of users involved in the raid.
        /// </summary>
        public ConcurrentHashSet<IGuildUser> RaidUsers { get; set; } = new();
    }

    /// <summary>
    /// Represents statistics related to anti-spam measures.
    /// </summary>
    public class AntiSpamStats
    {
        /// <summary>
        /// Gets or sets the anti-spam settings.
        /// </summary>
        public AntiSpamSetting AntiSpamSettings { get; set; }

        /// <summary>
        /// Gets or sets the statistics for each user involved in spamming.
        /// </summary>
        public ConcurrentDictionary<ulong, UserSpamStats> UserStats { get; set; }
            = new();
    }

    /// <summary>
    /// Represents statistics related to anti-alting measures.
    /// </summary>
    public class AntiAltStats
    {
        private readonly AntiAltSetting setting;
        private int counter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AntiAltStats"/> class with the specified anti-alt setting.
        /// </summary>
        /// <param name="setting">The anti-alt setting.</param>
        public AntiAltStats(AntiAltSetting setting) => this.setting = setting;

        /// <summary>
        /// Gets the action to be taken against alting.
        /// </summary>
        public PunishmentAction Action => setting.Action;

        /// <summary>
        /// Gets the duration of the action against alting in minutes.
        /// </summary>
        public int ActionDurationMinutes => setting.ActionDurationMinutes;

        /// <summary>
        /// Gets the ID of the role associated with alting punishment.
        /// </summary>
        public ulong? RoleId => setting.RoleId;

        /// <summary>
        /// Gets the minimum age required for a user to be considered as an alt.
        /// </summary>
        public string MinAge => setting.MinAge;

        /// <summary>
        /// Gets the counter for alting occurrences.
        /// </summary>
        public int Counter => counter;

        /// <summary>
        /// Increments the counter for alting occurrences.
        /// </summary>
        public void Increment() => Interlocked.Increment(ref counter);
    }
}