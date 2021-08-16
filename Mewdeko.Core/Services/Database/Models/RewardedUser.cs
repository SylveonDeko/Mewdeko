using System;

namespace Mewdeko.Core.Services.Database.Models
{
    public class RewardedUser : DbEntity
    {
        public ulong UserId { get; set; }
        public string PatreonUserId { get; set; }
        public int AmountRewardedThisMonth { get; set; }
        public DateTime LastReward { get; set; }
    }
}