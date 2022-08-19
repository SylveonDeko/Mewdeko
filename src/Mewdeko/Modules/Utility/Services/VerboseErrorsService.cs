using Discord.Commands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility.Services;

public class VerboseErrorsService : INService, IUnloadableService
{
    private readonly CommandHandler _ch;
    private readonly DbService _db;
    private readonly IBotStrings _strings;
    private readonly ConcurrentHashSet<ulong> _guildsEnabled;
    private readonly GuildSettingsService _guildSettings;
    private readonly IServiceProvider _services;

    public VerboseErrorsService(DiscordSocketClient client, DbService db, CommandHandler ch,
        IBotStrings strings,
        GuildSettingsService guildSettings,
        IServiceProvider services)
    {
        _strings = strings;
        _guildSettings = guildSettings;
        _services = services;
        _db = db;
        _ch = ch;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.All().Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        _ch.CommandErrored += LogVerboseError;

        _guildsEnabled = new ConcurrentHashSet<ulong>(gc
                                                        .Where(x => x.VerboseErrors)
                                                        .Select(x => x.GuildId));
    }

    public Task Unload()
    {
        _ch.CommandErrored -= LogVerboseError;
        return Task.CompletedTask;
    }

    private async Task LogVerboseError(CommandInfo cmd, ITextChannel? channel, string reason, IUser user)
    {
        if (channel == null || !_guildsEnabled.Contains(channel.GuildId))
            return;
        var perms = _services.GetService<PermissionService>();
        var pc = await perms.GetCacheFor(channel.GuildId);
        foreach (var i in cmd.Aliases)
        {
            if (!(pc.Permissions != null 
                  && pc.Permissions.CheckPermissions(new MewdekoUserMessage
                          { Author = user, Channel = channel }, 
                      i, 
                      cmd.MethodName(), out var index)))
                return;
        }
            
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("Command Error")
                .WithDescription(reason)
                .AddField("Usages",
                    string.Join("\n", cmd.RealRemarksArr(_strings, channel.Guild.Id, await _guildSettings.GetPrefix(channel.Guild))))
                .WithFooter($"Run {await _guildSettings.GetPrefix(channel.Guild.Id)}ve to disable these prompts.")
                .WithErrorColor();

            await channel.SendMessageAsync(embed: embed.Build(), components: new ComponentBuilder()
                                                                              .WithButton(label: "Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko").Build()).ConfigureAwait(false);
        }
        catch
        {
            //ignore
        }
    }

    public async Task<bool> ToggleVerboseErrors(ulong guildId, bool? enabled = null)
    {
        await using (var uow = _db.GetDbContext())
        {
            var gc = await uow.ForGuildId(guildId, set => set);

            if (enabled == null)
                enabled = gc.VerboseErrors = !gc.VerboseErrors; // Old behaviour, now behind a condition
            else gc.VerboseErrors = (bool)enabled; // New behaviour, just set it.

            await uow.SaveChangesAsync();
        }

        if ((bool)enabled) // This doesn't need to be duplicated inside the using block
            _guildsEnabled.Add(guildId);
        else
            _guildsEnabled.TryRemove(guildId);

        return (bool)enabled;
    }
}