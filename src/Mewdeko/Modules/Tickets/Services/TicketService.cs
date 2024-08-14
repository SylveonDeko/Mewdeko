using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Tickets.Services
{
    /// <summary>
    /// Tickets
    /// </summary>
    public class TicketService : INService
    {
        private readonly DbContextProvider dbProvider;
        private readonly DiscordShardedClient client;
        private readonly GuildSettingsService guildSettings;

        /// <summary>
        /// Tickets
        /// </summary>
        /// <param name="dbProvider"></param>
        /// <param name="client"></param>
        /// <param name="guildSettings"></param>
        public TicketService(
            DbContextProvider dbProvider,
            DiscordShardedClient client,
            GuildSettingsService guildSettings)
        {
            this.dbProvider = dbProvider;
            this.client = client;
            this.guildSettings = guildSettings;
        }

        /// <summary>
        /// Creates a ticket panel
        /// </summary>
        /// <param name="guildId"></param>
        /// <param name="channelId"></param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="buttons"></param>
        /// <returns></returns>
        public async Task<TicketPanel?> CreateTicketPanel(ulong guildId, ulong channelId, string messageCode)
        {
            await using var db = await dbProvider.GetContextAsync();
            var panels = await db.TicketPanels.FirstOrDefaultAsync(x => x.ChannelId == channelId);
            if (panels is not null)
                return null;
            var ticketPanel = new TicketPanel
            {
                GuildId = guildId,
                ChannelId = channelId,
                MessageJson = messageCode
            };

            db.TicketPanels.Add(ticketPanel);
            await db.SaveChangesAsync();

            return ticketPanel;
        }

        /// <summary>
        /// Chanel
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="user"></param>
        /// <param name="button"></param>
        /// <returns></returns>
        public async Task<ITextChannel> CreateTicketChannel(IGuild guild, IGuildUser user, TicketButton button)
        {
            var channelName = $"ticket-{user.Username.ToLower()}-{DateTime.Now:yyyyMMddHHmmss}";
            var ticketChannel = await guild.CreateTextChannelAsync(channelName, properties =>
            {
                properties.Topic = $"Ticket for {user.Username} - {button.Label}";
            });

            await ticketChannel.AddPermissionOverwriteAsync(user, OverwritePermissions.AllowAll(ticketChannel));
            await ticketChannel.AddPermissionOverwriteAsync(guild.EveryoneRole,
                OverwritePermissions.DenyAll(ticketChannel));

            var embed = new EmbedBuilder()
                .WithTitle($"New Ticket - {button.Label}")
                .WithDescription(button.OpenMessage)
                .WithColor(Color.Green)
                .WithFooter($"Ticket ID: {ticketChannel.Id}")
                .Build();

            await ticketChannel.SendMessageAsync(embed: embed);

            return ticketChannel;
        }

        /// <summary>
        /// Gets panels
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns></returns>
        public async Task<List<TicketPanel>> GetTicketPanels(ulong guildId)
        {
            await using var db = await dbProvider.GetContextAsync();
            return await db.TicketPanels
                .Where(tp => tp.GuildId == guildId)
                .Include(tp => tp.Buttons)
                .ToListAsync();
        }

        /// <summary>
        /// Yeets a panel
        /// </summary>
        /// <param name="guildId"></param>
        /// <param name="panelId"></param>
        public async Task DeleteTicketPanel(ulong guildId, int panelId)
        {
            await using var db = await dbProvider.GetContextAsync();
            var panel = await db.TicketPanels
                .Where(tp => tp.GuildId == guildId && tp.Id == panelId)
                .FirstOrDefaultAsync();

            if (panel != null)
            {
                db.TicketPanels.Remove(panel);
                await db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// getButtons lel
        /// </summary>
        /// <param name="buttonId"></param>
        /// <returns></returns>
        public async Task<TicketButton> GetTicketButton(int buttonId)
        {
            await using var db = await dbProvider.GetContextAsync();
            return await db.TicketButtons.FindAsync(buttonId);
        }
    }
}