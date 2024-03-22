using Discord.Commands;
using Mewdeko.Common.Collections;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service responsible for providing data to the admin module.
/// </summary>
public class AdministrationService : INService
{
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdministrationService"/> class with the specified dependencies.
    /// </summary>
    /// <param name="cmdHandler">The command handler.</param>
    /// <param name="db">The database service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="bot">The bot instance.</param>
    public AdministrationService(CommandHandler cmdHandler, DbService db,
        GuildSettingsService guildSettings, Mewdeko bot)
    {
        // Get all guild configurations from the bot
        var allgc = bot.AllGuildConfigs;

        // Create a new database context
        using var uow = db.GetDbContext();

        // Assign the database service and guild settings service
        this.db = db;
        this.guildSettings = guildSettings;

        // Initialize the DeleteMessagesOnCommand set with guild IDs where DeleteMessageOnCommand is set to 1
        DeleteMessagesOnCommand =
            new ConcurrentHashSet<ulong>(allgc.Where(g => g.DeleteMessageOnCommand == 1).Select(g => g.GuildId));

        // Initialize the DeleteMessagesOnCommandChannels dictionary with channel IDs and states from the guild configurations
        DeleteMessagesOnCommandChannels = new ConcurrentDictionary<ulong, bool>(allgc
            .SelectMany(x => x.DelMsgOnCmdChannels)
            .ToDictionary(x => x.ChannelId, x => x.State == 1)
            .ToConcurrent());

        // Subscribe to the CommandExecuted event of the command handler
        cmdHandler.CommandExecuted += DelMsgOnCmd_Handler;
    }


    /// <summary>
    /// A thread-safe set of guild IDs where DeleteMessageOnCommand is set to 1.
    /// </summary>
    private ConcurrentHashSet<ulong> DeleteMessagesOnCommand { get; }

    /// <summary>
    /// A thread-safe dictionary of channel IDs and states from the guild configurations.
    /// </summary>
    private ConcurrentDictionary<ulong, bool> DeleteMessagesOnCommandChannels { get; }

    /// <summary>
    /// Sets the staff role for a given guild.
    /// </summary>
    /// <param name="guild">The guild to set the staff role for.</param>
    /// <param name="role">The role to set as the staff role.</param>
    public async Task StaffRoleSet(IGuild guild, ulong role)
    {
        // Create a new database context
        await using var uow = db.GetDbContext();

        // Get the guild configuration for the given guild ID
        var gc = await uow.ForGuildId(guild.Id, set => set);

        // Set the staff role
        gc.StaffRole = role;

        // Save changes to the database
        await uow.SaveChangesAsync().ConfigureAwait(false);

        // Update the guild configuration in the cache
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Toggles the opt-out status for a given guild.
    /// </summary>
    /// <param name="guild">The guild to toggle the opt-out status for.</param>
    /// <returns>A boolean indicating the new opt-out status.</returns>
    public async Task<bool> ToggleOptOut(IGuild guild)
    {
        // Create a new database context
        await using var uow = db.GetDbContext();

        // Get the guild configuration for the given guild ID
        var gc = await uow.ForGuildId(guild.Id, set => set);

        // Toggle the opt-out status
        gc.StatsOptOut = gc.StatsOptOut == 0L ? 1L : 0L;

        // Save changes to the database
        await uow.SaveChangesAsync().ConfigureAwait(false);

        // Update the guild configuration in the cache
        await guildSettings.UpdateGuildConfig(guild.Id, gc);

        // Return the boolean equivalent of the new opt-out status
        return gc.StatsOptOut != 0;
    }


    /// <summary>
    /// Deletes the statistics data for a given guild.
    /// </summary>
    /// <param name="guild">The guild to delete the statistics data for.</param>
    /// <returns>A boolean indicating whether any data was deleted.</returns>
    public async Task<bool> DeleteStatsData(IGuild guild)
    {
        await using var uow = db.GetDbContext();
        var toRemove = uow.CommandStats.Where(x => x.GuildId == guild.Id).AsEnumerable();
        if (!toRemove.Any())
            return false;
        uow.CommandStats.RemoveRange(toRemove);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Sets the member role for a given guild.
    /// </summary>
    /// <param name="guild">The guild to set the member role for.</param>
    /// <param name="role">The role to set as the member role.</param>
    public async Task MemberRoleSet(IGuild guild, ulong role)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.MemberRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Gets the staff role for a given guild.
    /// </summary>
    /// <param name="id">The ID of the guild to get the staff role for.</param>
    /// <returns>The ID of the staff role.</returns>
    public async Task<ulong> GetStaffRole(ulong id) => (await guildSettings.GetGuildConfig(id)).StaffRole;

    /// <summary>
    /// Gets the member role for a given guild.
    /// </summary>
    /// <param name="id">The ID of the guild to get the member role for.</param>
    /// <returns>The ID of the member role.</returns>
    public async Task<ulong> GetMemberRole(ulong id) => (await guildSettings.GetGuildConfig(id)).MemberRole;

    /// <summary>
    /// Gets the DeleteMessageOnCommand data for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get the data for.</param>
    /// <returns>A tuple containing a boolean indicating the DeleteMessageOnCommand status and a collection of channels.</returns>
    public async Task<(bool DelMsgOnCmd, IEnumerable<DelMsgOnCmdChannel> channels)> GetDelMsgOnCmdData(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId,
            set => set.Include(x => x.DelMsgOnCmdChannels));

        return (false.ParseBoth(conf.DeleteMessageOnCommand.ToString()), conf.DelMsgOnCmdChannels);
    }

    /// <summary>
    /// Handles the deletion of messages on command execution.
    /// </summary>
    /// <param name="msg">The user message that triggered the command.</param>
    /// <param name="cmd">The executed command.</param>
    /// <returns>A completed task.</returns>
    private Task DelMsgOnCmd_Handler(IUserMessage msg, CommandInfo cmd)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Channel is SocketTextChannel channel)
            {
                if (DeleteMessagesOnCommandChannels.TryGetValue(channel.Id, out var state))
                {
                    // If the state is true and the command is not 'Purge' or 'pick', delete the message
                    if (state && cmd.Name != "Purge" && cmd.Name != "pick")
                    {
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                else if (DeleteMessagesOnCommand.Contains(channel.Guild.Id) && cmd.Name != "Purge" &&
                         cmd.Name != "pick")
                {
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Toggles the DeleteMessageOnCommand setting for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>A boolean indicating the new state of the DeleteMessageOnCommand setting.</returns>
    public async Task<bool> ToggleDeleteMessageOnCommand(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);

        // Toggle the value using a ternary operator
        conf.DeleteMessageOnCommand = conf.DeleteMessageOnCommand == 0L ? 1L : 0L;

        await guildSettings.UpdateGuildConfig(guildId, conf);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        // Return the boolean equivalent of the new value
        return conf.DeleteMessageOnCommand != 0;
    }


    /// <summary>
    /// Sets the DeleteMessageOnCommand state for a specific channel in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the state for.</param>
    /// <param name="chId">The ID of the channel to set the state for.</param>
    /// <param name="newState">The new state to set.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDelMsgOnCmdState(ulong guildId, ulong chId, Administration.State newState)
    {
        // Create a new database context
        await using var uow = db.GetDbContext();

        // Get the guild configuration for the given guild ID, including the DeleteMessageOnCommand channels
        var conf = await uow.ForGuildId(guildId,
            set => set.Include(x => x.DelMsgOnCmdChannels));

        // Get the existing state for the channel, if any
        var old = conf.DelMsgOnCmdChannels.FirstOrDefault(x => x.ChannelId == chId);

        // If the new state is 'Inherit', remove the existing state if it exists
        if (newState == Administration.State.Inherit)
        {
            if (old != null)
            {
                conf.DelMsgOnCmdChannels.Remove(old);
                uow.Remove(old);
            }
        }
        else
        {
            // If there is no existing state, create a new one
            if (old == null)
            {
                old = new DelMsgOnCmdChannel
                {
                    ChannelId = chId
                };
                conf.DelMsgOnCmdChannels.Add(old);
            }

            // Set the new state
            old.State = newState == Administration.State.Enable ? 1 : 0;
            DeleteMessagesOnCommandChannels[chId] = newState == Administration.State.Enable;
        }

        // Save changes to the database
        await uow.SaveChangesAsync().ConfigureAwait(false);

        // Update the DeleteMessagesOnCommandChannels dictionary based on the new state
        switch (newState)
        {
            case Administration.State.Disable:
                break;
            case Administration.State.Enable:
                DeleteMessagesOnCommandChannels[chId] = true; // true
                break;
            default:
                DeleteMessagesOnCommandChannels.TryRemove(chId, out _);
                break;
        }
    }


    /// <summary>
    /// Sets the deafen status for a list of users in a guild.
    /// </summary>
    /// <param name="value">The deafen status to set. If true, the users will be deafened. If false, the users will be undeafened.</param>
    /// <param name="users">The users to set the deafen status for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task DeafenUsers(bool value, params IGuildUser[] users)
    {
        if (users.Length == 0)
            return;
        foreach (var u in users)
        {
            try
            {
                await u.ModifyAsync(usr => usr.Deaf = value).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    /// Edits a message in a text channel.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="chanl">The text channel where the message is located.</param>
    /// <param name="messageId">The ID of the message to edit.</param>
    /// <param name="text">The new text for the message. If null, the message content will be removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task EditMessage(ICommandContext context, ITextChannel chanl, ulong messageId, string? text)
    {
        var msg = await chanl.GetMessageAsync(messageId).ConfigureAwait(false);

        if (msg is not IUserMessage umsg || msg.Author.Id != context.Client.CurrentUser.Id)
            return;

        var rep = new ReplacementBuilder()
            .WithDefault(context)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(text), context.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            await umsg.ModifyAsync(x =>
            {
                x.Embeds = embed;
                x.Content = plainText?.SanitizeMentions();
                x.Components = components.Build();
            }).ConfigureAwait(false);
        }
        else
        {
            await umsg.ModifyAsync(x =>
            {
                x.Content = text.SanitizeMentions();
                x.Embed = null;
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }
}