using NadekoBot.Extensions;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class WaifuInfo : DbEntity
    {
        public int WaifuId { get; set; }
        public DiscordUser Waifu { get; set; }

        public int? ClaimerId { get; set; }
        public DiscordUser Claimer { get; set; }

        public int? AffinityId { get; set; }
        public DiscordUser Affinity { get; set; }

        public int Price { get; set; }
        public List<WaifuItem> Items { get; set; } = new List<WaifuItem>();

        public override string ToString()
        {
            var claimer = "no one";
            var status = "";

            var waifuUsername = Waifu.Username.TrimTo(20);
            var claimerUsername = Claimer?.Username.TrimTo(20);

            if (ClaimerId != null)
            {
                claimer = $"{ claimerUsername }#{Claimer.Discriminator}";
            }
            if (AffinityId == null)
            {
                status = $"... but {waifuUsername}'s heart is empty";
            }
            else if (AffinityId == ClaimerId)
            {
                status = $"... and {waifuUsername} likes {claimerUsername} too <3";
            }
            else
            {
                status = $"... but {waifuUsername}'s heart belongs to {Affinity.Username.TrimTo(20)}#{Affinity.Discriminator}";
            }
            return $"**{waifuUsername}#{Waifu.Discriminator}** - claimed by **{claimer}**\n\t{status}";
        }
    }

    public class WaifuLbResult
    {
        public string Username { get; set; }
        public string Discrim { get; set; }

        public string Claimer { get; set; }
        public string ClaimerDiscrim { get; set; }

        public string Affinity { get; set; }
        public string AffinityDiscrim { get; set; }

        public int Price { get; set; }

        public override string ToString()
        {
            var claimer = "no one";
            var status = "";

            var waifuUsername = Username.TrimTo(20);
            var claimerUsername = Claimer?.TrimTo(20);

            if (Claimer != null)
            {
                claimer = $"{ claimerUsername }#{ClaimerDiscrim}";
            }
            if (Affinity == null)
            {
                status = $"... but {waifuUsername}'s heart is empty";
            }
            else if (Affinity + AffinityDiscrim == Claimer + ClaimerDiscrim)
            {
                status = $"... and {waifuUsername} likes {claimerUsername} too <3";
            }
            else
            {
                status = $"... but {waifuUsername}'s heart belongs to {Affinity.TrimTo(20)}#{AffinityDiscrim}";
            }
            return $"**{waifuUsername}#{Discrim}** - claimed by **{claimer}**\n\t{status}";
        }
    }
}