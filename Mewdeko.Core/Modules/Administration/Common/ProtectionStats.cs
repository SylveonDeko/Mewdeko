using System.Collections.Concurrent;
using Discord;
using Mewdeko.Common.Collections;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Modules.Administration.Common
{
    public enum ProtectionType
    {
        Raiding,
        Spamming
    }

    public class AntiRaidStats
    {
        public AntiRaidSetting AntiRaidSettings { get; set; }
        public int UsersCount { get; set; }
        public ConcurrentHashSet<IGuildUser> RaidUsers { get; set; } = new();
    }

    public class AntiSpamStats
    {
        public AntiSpamSetting AntiSpamSettings { get; set; }

        public ConcurrentDictionary<ulong, UserSpamStats> UserStats { get; set; }
            = new();
    }
}