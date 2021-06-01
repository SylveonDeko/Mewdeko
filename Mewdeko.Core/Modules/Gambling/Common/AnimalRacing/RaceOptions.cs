using CommandLine;
using Mewdeko.Core.Common;

namespace Mewdeko.Core.Modules.Gambling.Common.AnimalRacing
{
    public class RaceOptions : IMewdekoCommandOptions
    {
        [Option('s', "start-time", Default = 20, Required = false)]
        public int StartTime { get; set; } = 20;

        public void NormalizeOptions()
        {
            if (this.StartTime < 10 || this.StartTime > 120)
                this.StartTime = 20;
        }
    }
}