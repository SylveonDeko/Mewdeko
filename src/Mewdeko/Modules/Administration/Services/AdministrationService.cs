using Discord.Commands;
using Mewdeko.Common.Collections;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

public class AdministrationService : INService
{
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    public AdministrationService(CommandHandler cmdHandler, DbService db,
        GuildSettingsService guildSettings, Mewdeko bot)
    {
        var allgc = bot.AllGuildConfigs;
        using var uow = db.GetDbContext();
        this.db = db;
        this.guildSettings = guildSettings;

        DeleteMessagesOnCommand =
            new ConcurrentHashSet<ulong>(allgc.Where(g => g.DeleteMessageOnCommand == 1).Select(g => g.GuildId));

        DeleteMessagesOnCommandChannels = new ConcurrentDictionary<ulong, bool>(allgc
            .SelectMany(x => x.DelMsgOnCmdChannels)
            .ToDictionary(x => x.ChannelId, x => x.State == 1)
            .ToConcurrent());

        cmdHandler.CommandExecuted += DelMsgOnCmd_Handler;
    }


    private ConcurrentHashSet<ulong> DeleteMessagesOnCommand { get; }
    private ConcurrentDictionary<ulong, bool> DeleteMessagesOnCommandChannels { get; }

    public async Task StaffRoleSet(IGuild guild, ulong role)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StaffRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<bool> ToggleOptOut(IGuild guild)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);

        // Use a ternary operator to toggle the value
        gc.StatsOptOut = gc.StatsOptOut == 0L ? 1L : 0L;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);

        // Return the boolean equivalent of the new value
        return gc.StatsOptOut != 0;
    }


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

    public async Task MemberRoleSet(IGuild guild, ulong role)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.MemberRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<ulong> GetStaffRole(ulong id) => (await guildSettings.GetGuildConfig(id)).StaffRole;

    public async Task<ulong> GetMemberRole(ulong id) => (await guildSettings.GetGuildConfig(id)).MemberRole;

    public async Task<(bool DelMsgOnCmd, IEnumerable<DelMsgOnCmdChannel> channels)> GetDelMsgOnCmdData(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId,
            set => set.Include(x => x.DelMsgOnCmdChannels));

        return (false.ParseBoth(conf.DeleteMessageOnCommand.ToString()), conf.DelMsgOnCmdChannels);
    }

    private Task DelMsgOnCmd_Handler(IUserMessage msg, CommandInfo cmd)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Channel is SocketTextChannel channel)
            {
                if (DeleteMessagesOnCommandChannels.TryGetValue(channel.Id, out var state))
                {
                    if (state && cmd.Name != "Purge" && cmd.Name != "pick")
                    {
                        //logService.AddDeleteIgnore(msg.Id);
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    //if state is false, that means do not do it
                }
                else if (DeleteMessagesOnCommand.Contains(channel.Guild.Id) && cmd.Name != "Purge" &&
                         cmd.Name != "pick")
                {
                    //logService.AddDeleteIgnore(msg.Id);
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

            //wat ?!
        });
        return Task.CompletedTask;
    }

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


    public async Task SetDelMsgOnCmdState(ulong guildId, ulong chId, Administration.State newState)
    {
        await using var uow = db.GetDbContext();

        var conf = await uow.ForGuildId(guildId,
            set => set.Include(x => x.DelMsgOnCmdChannels));

        var old = conf.DelMsgOnCmdChannels.FirstOrDefault(x => x.ChannelId == chId);

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
            if (old == null)
            {
                old = new DelMsgOnCmdChannel
                {
                    ChannelId = chId
                };
                conf.DelMsgOnCmdChannels.Add(old);
            }

            old.State = newState == Administration.State.Enable ? 1 : 0;
            DeleteMessagesOnCommandChannels[chId] = newState == Administration.State.Enable;
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);

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