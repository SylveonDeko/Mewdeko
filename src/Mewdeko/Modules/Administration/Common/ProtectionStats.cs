using System.Threading;
using Mewdeko.Common.Collections;

namespace Mewdeko.Modules.Administration.Common;

public enum ProtectionType
{
    Raiding,
    Spamming,
    Alting,
    MassMention
}

public class AntiRaidStats
{
    public AntiRaidSetting AntiRaidSettings { get; set; }
    public int UsersCount { get; set; }
    public ConcurrentHashSet<IGuildUser> RaidUsers { get; set; } = new();
}

public class AntiMassMentionStats
{
}

public class AntiSpamStats
{
    public AntiSpamSetting AntiSpamSettings { get; set; }

    public ConcurrentDictionary<ulong, UserSpamStats> UserStats { get; set; }
        = new();
}

public class AntiAltStats
{
    private readonly AntiAltSetting setting;

    private int counter;

    public AntiAltStats(AntiAltSetting setting) => this.setting = setting;

    public PunishmentAction Action => setting.Action;
    public int ActionDurationMinutes => setting.ActionDurationMinutes;
    public ulong? RoleId => setting.RoleId;
    public TimeSpan MinAge => setting.MinAge;
    public int Counter => counter;

    public void Increment() => Interlocked.Increment(ref counter);
}