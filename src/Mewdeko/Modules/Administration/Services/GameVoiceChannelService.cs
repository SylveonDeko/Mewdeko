using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

public class GameVoiceChannelService : INService
{
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    public GameVoiceChannelService(DbService db,
        GuildSettingsService guildSettings, EventHandler eventHandler)
    {
        this.db = db;
        this.guildSettings = guildSettings;

        eventHandler.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        eventHandler.GuildMemberUpdated += _client_GuildMemberUpdated;
    }

    private async Task _client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser? after)
    {
        try
        {
            if (after is null)
                return;
            if ((await guildSettings.GetGuildConfig(after.Guild.Id)).GameVoiceChannel != after?.VoiceChannel?.Id)
                return;
            //if the user is in the voice channel and that voice channel is gvc
            //if the activity has changed, and is a playing activity
            if (!cacheable.HasValue)
                return;
            if (!Equals(cacheable.Value.Activities, after.Activities)
                && after.Activities != null
                && after.Activities.FirstOrDefault()?.Type == ActivityType.Playing)
            {
                //trigger gvc
                await TriggerGvc(after, after.Activities.FirstOrDefault()?.Name).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error running GuildMemberUpdated in gvc");
        }
    }

    public async Task<ulong?> ToggleGameVoiceChannel(ulong guildId, ulong vchId)
    {
        ulong? id;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);

        if (gc.GameVoiceChannel == vchId)
        {
            id = gc.GameVoiceChannel = 0;
            guildSettings.UpdateGuildConfig(guildId, gc);
        }
        else
        {
            id = gc.GameVoiceChannel = vchId;
            guildSettings.UpdateGuildConfig(guildId, gc);
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return id;
    }

    private async Task Client_UserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState, SocketVoiceState newState)
    {
        try
        {
            if (usr is not SocketGuildUser gUser)
                return;

            var game = gUser.Activities.FirstOrDefault()?.Name;

            if (oldState.VoiceChannel == newState.VoiceChannel ||
                newState.VoiceChannel == null)
            {
                return;
            }

            if ((await guildSettings.GetGuildConfig(gUser.Guild.Id)).GameVoiceChannel != newState.VoiceChannel.Id ||
                string.IsNullOrWhiteSpace(game))
            {
                return;
            }

            await TriggerGvc(gUser, game).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error running VoiceStateUpdate in gvc");
        }
    }

    private static async Task TriggerGvc(SocketGuildUser gUser, string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return;

        game = game.TrimTo(50).ToLowerInvariant();
        var vch = gUser.Guild.VoiceChannels
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == game);

        if (vch == null)
            return;

        await Task.Delay(1000).ConfigureAwait(false);
        await gUser.ModifyAsync(gu => gu.Channel = vch).ConfigureAwait(false);
    }
}