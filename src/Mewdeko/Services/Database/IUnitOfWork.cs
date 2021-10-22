using System;
using System.Threading.Tasks;
using Mewdeko.Services.Database.Repositories;

namespace Mewdeko.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        MewdekoContext _context { get; }

        IQuoteRepository Quotes { get; }
        IGuildConfigRepository GuildConfigs { get; }
        IReminderRepository Reminders { get; }
        ISelfAssignedRolesRepository SelfAssignedRoles { get; }

        ICustomReactionRepository CustomReactions { get; }

        IMusicPlaylistRepository MusicPlaylists { get; }
        IWaifuRepository Waifus { get; }
        ITicketRepository Tickets { get; }
        IDiscordUserRepository DiscordUsers { get; }
        IGlobalBansRepository GlobalBans { get; }
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