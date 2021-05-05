using System.Threading.Tasks;
using NadekoBot.Core.Services;
using System.Collections.Concurrent;
using NadekoBot.Modules.Gambling.Common.AnimalRacing;

namespace NadekoBot.Modules.Gambling.Services
{
    public class AnimalRaceService : INService, IUnloadableService
    {
        public ConcurrentDictionary<ulong, AnimalRace> AnimalRaces { get; } = new ConcurrentDictionary<ulong, AnimalRace>();

        public Task Unload()
        {
            foreach (var kvp in AnimalRaces)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            return Task.CompletedTask;
        }
    }
}
