using NadekoBot.Modules.Xp.Services;

namespace NadekoBot.Modules.Xp.Common
{
    public class LevelStats
    {
        public int Level { get; }
        public int LevelXp { get; }
        public int RequiredXp { get; }
        public int TotalXp { get; }

        public LevelStats(int xp)
        {
            if (xp < 0)
                xp = 0;

            TotalXp = xp;

            const int baseXp = XpService.XP_REQUIRED_LVL_1;

            var required = baseXp;
            var totalXp = 0;
            var lvl = 1;
            while (true)
            {
                required = (int)(baseXp + baseXp / 4.0 * (lvl - 1));

                if (required + totalXp > xp)
                    break;

                totalXp += required;
                lvl++;
            }

            Level = lvl - 1;
            LevelXp = xp - totalXp;
            RequiredXp = required;
        }
    }
}
