using System.Collections.Concurrent;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Mewdeko.Modules.Gambling.Common.AnimalRacing;

namespace Mewdeko.Modules.Gambling.Services
{
    public class AnimalRaceService : INService, IUnloadableService
    {
        public ConcurrentDictionary<ulong, AnimalRace> AnimalRaces { get; } = new();

        public Task Unload()
        {
            foreach (var kvp in AnimalRaces)
                try
                {
                    kvp.Value.Dispose();
                }
                catch
                {
                }

            return Task.CompletedTask;
        }
    }
}