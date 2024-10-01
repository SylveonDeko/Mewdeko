using Mewdeko.Database.DbContextStuff;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing game voice channels.
/// </summary>
public class GameVoiceChannelService : INService
{
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Constructs a new instance of the GameVoiceChannelService.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public GameVoiceChannelService(DbContextProvider dbProvider,
        GuildSettingsService guildSettings, EventHandler eventHandler)
    {
        this.dbProvider = dbProvider;
        this.guildSettings = guildSettings;

        eventHandler.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        eventHandler.GuildMemberUpdated += _client_GuildMemberUpdated;
    }

    /// <summary>
    ///     Handles the GuildMemberUpdated event.
    /// </summary>
    /// <param name="cacheable">The cacheable guild user.</param>
    /// <param name="after">The guild user after the update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    ///     Toggles the game voice channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="vchId">The ID of the voice channel.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the ID of the toggled game voice
    ///     channel.
    /// </returns>
    public async Task<ulong?> ToggleGameVoiceChannel(ulong guildId, ulong vchId)
    {
        ulong? id;

        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guildId, set => set);

        if (gc.GameVoiceChannel == vchId)
        {
            id = gc.GameVoiceChannel = 0;
            await guildSettings.UpdateGuildConfig(guildId, gc);
        }
        else
        {
            id = gc.GameVoiceChannel = vchId;
            await guildSettings.UpdateGuildConfig(guildId, gc);
        }

        return id;
    }

    /// <summary>
    ///     Handles the UserVoiceStateUpdated event.
    /// </summary>
    /// <param name="usr">The user whose voice state was updated.</param>
    /// <param name="oldState">The old voice state.</param>
    /// <param name="newState">The new voice state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task Client_UserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState,
        SocketVoiceState newState)
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


    /// <summary>
    ///     Triggers the game voice channel for a guild user.
    /// </summary>
    /// <param name="gUser">The guild user.</param>
    /// <param name="game">The game.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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