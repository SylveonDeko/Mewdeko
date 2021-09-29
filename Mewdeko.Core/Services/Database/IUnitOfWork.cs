using System;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Repositories;

namespace Mewdeko.Core.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        MewdekoContext _context { get; }

        IQuoteRepository Quotes { get; }
        IGuildConfigRepository GuildConfigs { get; }
        IReminderRepository Reminders { get; }
        ISelfAssignedRolesRepository SelfAssignedRoles { get; }

        ICustomReactionRepository CustomReactions { get; }

        // ISwitchShopsRepository SwitchShops {get;}
        IMusicPlaylistRepository MusicPlaylists { get; }
        IWaifuRepository Waifus { get; }
        ITicketRepository Tickets { get; }
        IDiscordUserRepository DiscordUsers { get; }
        IReputationRepository Reputation { get; }
        IWarningsRepository Warnings { get; }
        IGiveawaysRepository Giveaways { get; }
        IXpRepository Xp { get; }
        IClubRepository Clubs { get; }
        ISuggestionsRepository Suggestions { get; }
        IStarboardRepository Starboard { get; }
        ISnipeStoreRepository SnipeStore { get; }
        IAFKRepository AFK { get; }
        IWarningsRepository2 Warnings2 { get; }
        IPollsRepository Polls { get; }
        IPlantedCurrencyRepository PlantedCurrency { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}