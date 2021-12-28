using System;
using System.Threading.Tasks;
using Mewdeko.Services.Database.Repositories;
using Mewdeko.Services.Database.Repositories.Impl;

namespace Mewdeko.Services.Database;

public sealed class UnitOfWork : IUnitOfWork
{
    private IAfkRepository _AFK;

    private IClubRepository _clubs;

    private ICustomReactionRepository _customReactions;

    private IDiscordUserRepository _discordUsers;
    private IGiveawaysRepository _giveaways;
    private IGlobalBansRepository _globalbans;

    private IGuildConfigRepository _guildConfigs;

    private IMusicPlaylistRepository _musicPlaylists;

    private IPlantedCurrencyRepository _planted;

    private IPollsRepository _polls;
    private IQuoteRepository _quotes;
    private IReminderRepository _reminders;

    private ISelfAssignedRolesRepository _selfAssignedRoles;
    private ISnipeStoreRepository _snipestore;
    private IStarboardRepository _Starboard;
    private ISuggestionsRepository _suggestions;
    private ITicketRepository _tickets;

    private IWaifuRepository _waifus;

    private IWarningsRepository _warnings;

    private IWarningsRepository2 _warnings2;

    private IXpRepository _xp;

    public UnitOfWork(MewdekoContext context)
    {
        _context = context;
    }

    public MewdekoContext _context { get; }
    public IQuoteRepository Quotes => _quotes ??= new QuoteRepository(_context);


    public IGuildConfigRepository GuildConfigs =>
        _guildConfigs ??= new GuildConfigRepository(_context);

    public IGlobalBansRepository GlobalBans => _globalbans ??= new GlobalBansRepository(_context);

    public IReminderRepository Reminders => _reminders ??= new ReminderRepository(_context);

    public ISelfAssignedRolesRepository SelfAssignedRoles =>
        _selfAssignedRoles ??= new SelfAssignedRolesRepository(_context);

    public ITicketRepository Tickets => _tickets ??= new TicketRepository(_context);

    public IMusicPlaylistRepository MusicPlaylists =>
        _musicPlaylists ??= new MusicPlaylistRepository(_context);

    public ICustomReactionRepository CustomReactions =>
        _customReactions ??= new CustomReactionsRepository(_context);

    public IWaifuRepository Waifus => _waifus ??= new WaifuRepository(_context);

    public IDiscordUserRepository DiscordUsers =>
        _discordUsers ??= new DiscordUserRepository(_context);

    public IGiveawaysRepository Giveaways => _giveaways ??= new GiveawayRepository(_context);
    public IWarningsRepository Warnings => _warnings ??= new WarningsRepository(_context);
    public IWarningsRepository2 Warnings2 => _warnings2 ??= new WarningsRepository2(_context);
    public ISuggestionsRepository Suggestions => _suggestions ??= new SuggestRepository(_context);

    public ISnipeStoreRepository SnipeStore => _snipestore ??= new SnipeStoreRepository(_context);
    public IAfkRepository AFK => _AFK ??= new AFKRepository(_context);
    public IStarboardRepository Starboard => _Starboard ??= new StarboardRepository(_context);
    public IXpRepository Xp => _xp ??= new XpRepository(_context);
    public IClubRepository Clubs => _clubs ??= new ClubRepository(_context);
    public IPollsRepository Polls => _polls ??= new PollsRepository(_context);

    public IPlantedCurrencyRepository PlantedCurrency =>
        _planted ??= new PlantedCurrencyRepository(_context);

    public int SaveChanges()
    {
        return _context.SaveChanges();
    }
    //it saved everything as null, that or it added an entry along with GuildCOnfigs

    public Task<int> SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}