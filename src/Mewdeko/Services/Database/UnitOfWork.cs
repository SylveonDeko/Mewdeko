using Mewdeko.Services.Database.Repositories;
using Mewdeko.Services.Database.Repositories.Impl;

namespace Mewdeko.Services.Database;

public sealed class UnitOfWork : IUnitOfWork
{
    private IAfkRepository afk;

    private IClubRepository clubs;

    private ICustomReactionRepository customReactions;

    private IDiscordUserRepository discordUsers;
    private IGiveawaysRepository giveaways;
    private IGlobalBansRepository globalbans;

    private IGuildConfigRepository guildConfigs;

    private IMusicPlaylistRepository musicPlaylists;
    private IMultiGreetRepository multiGreets;

    private IPlantedCurrencyRepository planted;

    private IPollsRepository polls;
    private IQuoteRepository quotes;
    private IReminderRepository reminders;

    private ISelfAssignedRolesRepository selfAssignedRoles;
    private ISnipeStoreRepository snipestore;
    private IStarboardRepository starboard;
    private ISuggestionsRepository suggestions;
    private ITicketRepository tickets;

    private IWaifuRepository waifus;

    private IWarningsRepository warnings;

    private IWarningsRepository2 warnings2;

    private IXpRepository xp;

    public UnitOfWork(MewdekoContext context) => Context = context;

    public MewdekoContext Context { get; }
    public IQuoteRepository Quotes => quotes ??= new QuoteRepository(Context);


    public IGuildConfigRepository GuildConfigs =>
        guildConfigs ??= new GuildConfigRepository(Context);

    public IGlobalBansRepository GlobalBans => globalbans ??= new GlobalBansRepository(Context);

    public IReminderRepository Reminders => reminders ??= new ReminderRepository(Context);
    public IMultiGreetRepository MultiGreets => multiGreets ??= new MultiGreetRepository(Context);

    public ISelfAssignedRolesRepository SelfAssignedRoles =>
        selfAssignedRoles ??= new SelfAssignedRolesRepository(Context);

    public ITicketRepository Tickets => tickets ??= new TicketRepository(Context);

    public IMusicPlaylistRepository MusicPlaylists =>
        musicPlaylists ??= new MusicPlaylistRepository(Context);

    public ICustomReactionRepository CustomReactions =>
        customReactions ??= new CustomReactionsRepository(Context);

    public IWaifuRepository Waifus => waifus ??= new WaifuRepository(Context);

    public IDiscordUserRepository DiscordUsers =>
        discordUsers ??= new DiscordUserRepository(Context);

    public IGiveawaysRepository Giveaways => giveaways ??= new GiveawayRepository(Context);
    public IWarningsRepository Warnings => warnings ??= new WarningsRepository(Context);
    public IWarningsRepository2 Warnings2 => warnings2 ??= new WarningsRepository2(Context);
    public ISuggestionsRepository Suggestions => suggestions ??= new SuggestRepository(Context);

    public ISnipeStoreRepository SnipeStore => snipestore ??= new SnipeStoreRepository(Context);
    public IAfkRepository AFK => afk ??= new AfkRepository(Context);
    public IStarboardRepository Starboard => starboard ??= new StarboardRepository(Context);
    public IXpRepository Xp => xp ??= new XpRepository(Context);
    public IClubRepository Clubs => clubs ??= new ClubRepository(Context);
    public IPollsRepository Polls => polls ??= new PollsRepository(Context);

    public IPlantedCurrencyRepository PlantedCurrency =>
        planted ??= new PlantedCurrencyRepository(Context);

    public int SaveChanges() => Context.SaveChanges();
    //it saved everything as null, that or it added an entry along with GuildCOnfigs

    public Task<int> SaveChangesAsync() => Context.SaveChangesAsync();

    public void Dispose()
    {
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}