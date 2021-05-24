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
        IBotConfigRepository BotConfig { get; }
        ICustomReactionRepository CustomReactions { get; }
        IMusicPlaylistRepository MusicPlaylists { get; }
        ICurrencyTransactionsRepository CurrencyTransactions { get; }
        IWaifuRepository Waifus { get; }
        IDiscordUserRepository DiscordUsers { get; }
        IWarningsRepository Warnings { get; }
        ISuggestionsRepository Suggestions { get; }
        IStarboardRepository Starboard { get; }
        ISnipeStoreRepository SnipeStore { get; }
        IAFKRepository AFK { get; }
        IWarningsRepository2 Warnings2 { get; }
        IXpRepository Xp { get; }
        IClubRepository Clubs { get; }
        IPollsRepository Polls { get; }
        IPlantedCurrencyRepository PlantedCurrency { get; }

        int SaveChanges();
        Task<int> SaveChangesAsync();
    }
}