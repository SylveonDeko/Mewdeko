using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class XpSettings : DbEntity
    {
        public int GuildConfigId { get; set; }
        public GuildConfig GuildConfig { get; set; }

        public HashSet<XpRoleReward> RoleRewards { get; set; } = new HashSet<XpRoleReward>();
        public HashSet<XpCurrencyReward> CurrencyRewards { get; set; } = new HashSet<XpCurrencyReward>();
        public bool XpRoleRewardExclusive { get; set; }
        public string NotifyMessage { get; set; } = "Congratulations {0}! You have reached level {1}!";
        public HashSet<ExcludedItem> ExclusionList { get; set; } = new HashSet<ExcludedItem>();
        public bool ServerExcluded { get; set; }
    }

    public enum ExcludedItemType { Channel, Role }

    public class XpRoleReward : DbEntity
    {
        public int XpSettingsId { get; set; }
        public XpSettings XpSettings { get; set; }

        public int Level { get; set; }
        public ulong RoleId { get; set; }

        public override int GetHashCode()
        {
            return Level.GetHashCode() ^ XpSettingsId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is XpRoleReward xrr && xrr.Level == Level && xrr.XpSettingsId == XpSettingsId;
        }
    }

    public class XpCurrencyReward : DbEntity
    {
        public int XpSettingsId { get; set; }
        public XpSettings XpSettings { get; set; }

        public int Level { get; set; }
        public int Amount { get; set; }

        public override int GetHashCode()
        {
            return Level.GetHashCode() ^ XpSettingsId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is XpCurrencyReward xrr && xrr.Level == Level && xrr.XpSettingsId == XpSettingsId;
        }
    }

    public class ExcludedItem : DbEntity
    {
        public ulong ItemId { get; set; }
        public ExcludedItemType ItemType { get; set; }

        public override int GetHashCode()
        {
            return ItemId.GetHashCode() ^ ItemType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is ExcludedItem ei && ei.ItemId == ItemId && ei.ItemType == ItemType;
        }
    }
}
