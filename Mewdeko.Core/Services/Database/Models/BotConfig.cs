using System;
using System.Collections.Generic;
using Discord;
using Mewdeko.Common.Collections;

namespace Mewdeko.Core.Services.Database.Models
{
    public class BotConfig : DbEntity
    {
        public bool HasMigratedBotSettings { get; set; } = true;

        public HashSet<BlacklistItem> Blacklist { get; set; }

        public float PatreonCurrencyPerCent { get; set; } = 1.0f;
        public float CurrencyGenerationChance { get; set; } = 0.02f;
        public int CurrencyGenerationCooldown { get; set; } = 10;

        public List<PlayingStatus> RotatingStatusMessages { get; set; } = new();

        public string RemindMessageFormat { get; set; } =
            "❗⏰**I've been told to remind you to '%message%' now by %user%.**⏰❗";

        //currency
        public string CurrencySign { get; set; } = "🌸";
        public string CurrencyName { get; set; } = "Mewdeko Flower";

        public int TriviaCurrencyReward { get; set; } = 0;
        public float BetflipMultiplier { get; set; } = 1.95f;
        public int CurrencyDropAmount { get; set; } = 1;
        public int? CurrencyDropAmountMax { get; set; } = null;
        public float Betroll67Multiplier { get; set; } = 2;
        public float Betroll91Multiplier { get; set; } = 4;
        public float Betroll100Multiplier { get; set; } = 10;
        public int TimelyCurrency { get; set; } = 0;
        public int TimelyCurrencyPeriod { get; set; } = 0;
        public float DailyCurrencyDecay { get; set; } = 0;
        public DateTime LastCurrencyDecay { get; set; } = DateTime.MinValue;
        public int MinWaifuPrice { get; set; } = 50;

        public HashSet<EightBallResponse> EightBallResponses { get; set; } = new();
        public HashSet<RaceAnimal> RaceAnimals { get; set; } = new();
        public IndexedCollection<StartupCommand> StartupCommands { get; set; }
        public bool CustomReactionsStartWith { get; set; } = false;
        public int XpPerMessage { get; set; } = 3;
        public int XpMinutesTimeout { get; set; } = 5;
        public double VoiceXpPerMinute { get; set; } = 0;
        public int MaxXpMinutes { get; set; } = 720;
        public int DivorcePriceMultiplier { get; set; } = 150;
        public int WaifuGiftMultiplier { get; set; } = 1;
        public int MinimumTriviaWinReq { get; set; }
        public int MinBet { get; set; } = 0;
        public int MaxBet { get; set; } = 0;
        public bool CurrencyGenerationPassword { get; set; }


        /// <summary> UNUSED </summary>
        [Obsolete("Use MinBet instead.")]
        public int MinimumBetAmount { get; set; } = 2;

        #region Obsolete/UNUSED

        public UpdateCheckType CheckForUpdates { get; set; } = UpdateCheckType.Release;
        public string CurrencyPluralName { get; set; } = "Mewdeko Flowers";
        public int MigrationVersion { get; set; }
        public int PermissionVersion { get; set; } = 2;

        public string UpdateString { get; set; } = "New update has been released.";
        public DateTime LastUpdate { get; set; } = new(2018, 5, 5, 0, 0, 0, DateTimeKind.Utc);
        public ulong BufferSize { get; set; } = 4000000;

        #endregion

        #region Obsolete, Moved to bot.yml

        public string OkColor { get; set; } = "00e584";
        public string ErrorColor { get; set; } = "ee281f";
        public string Locale { get; set; } = null;
        public OBSOLETE_ConsoleOutputType ConsoleOutputType { get; set; } = OBSOLETE_ConsoleOutputType.Normal;
        public bool ForwardMessages { get; set; } = true;
        public bool ForwardToAllOwners { get; set; } = true;
        public HashSet<BlockedCmdOrMdl> BlockedCommands { get; set; }
        public HashSet<BlockedCmdOrMdl> BlockedModules { get; set; }
        public string DefaultPrefix { get; set; } = ".";
        public bool GroupGreets { get; set; }
        public string DMHelpString { get; set; } = "Type `.h` for help.";

        public string HelpString { get; set; } =
            @"To add me to your server, use this link -> <https://discordapp.com/oauth2/authorize?client_id={0}&scope=bot&permissions=66186303>
You can use `{1}modules` command to see a list of all modules.
You can use `{1}commands ModuleName` to see a list of all of the commands in that module.
(for example `{1}commands Admin`) 
For a specific command help, use `{1}h CommandName` (for example {1}h {1}q)


**LIST OF COMMANDS CAN BE FOUND ON THIS LINK**
<https://Mewdeko.bot/commands>


Mewdeko Support Server: https://discord.gg/Mewdeko";

        public bool RotatingStatuses { get; set; } = false;

        #endregion
    }

    public enum UpdateCheckType
    {
        Release,
        Commit,
        None
    }

    public class BlockedCmdOrMdl : DbEntity
    {
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            return (obj as BlockedCmdOrMdl)?.Name?.ToUpperInvariant() == Name.ToUpperInvariant();
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode(StringComparison.InvariantCulture);
        }
    }

    public enum OBSOLETE_ConsoleOutputType
    {
        Normal,
        Simple
    }

    public class StartupCommand : DbEntity, IIndexed
    {
        public string CommandText { get; set; }
        public ulong ChannelId { get; set; }
        public string ChannelName { get; set; }
        public ulong? GuildId { get; set; }
        public string GuildName { get; set; }
        public ulong? VoiceChannelId { get; set; }
        public string VoiceChannelName { get; set; }
        public int Interval { get; set; }
        public int Index { get; set; }
    }

    public class PlayingStatus : DbEntity
    {
        public string Status { get; set; }
        public ActivityType Type { get; set; }
    }

    public class BlacklistItem : DbEntity
    {
        public ulong ItemId { get; set; }
        public BlacklistType Type { get; set; }
    }

    public enum BlacklistType
    {
        Server,
        Channel,
        User
    }

    public class EightBallResponse : DbEntity
    {
        public string Text { get; set; }

        public override int GetHashCode()
        {
            return Text.GetHashCode(StringComparison.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return obj is EightBallResponse response
                ? response.Text == Text
                : base.Equals(obj);
        }
    }

    public class RaceAnimal : DbEntity
    {
        public string Icon { get; set; }
        public string Name { get; set; }

        public override int GetHashCode()
        {
            return Icon.GetHashCode(StringComparison.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return obj is RaceAnimal animal
                ? animal.Icon == Icon
                : base.Equals(obj);
        }
    }
}