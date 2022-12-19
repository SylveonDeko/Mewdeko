using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.Utility.Services;

public class VerboseErrorsService : INService, IUnloadableService
{
    private readonly CommandHandler ch;
    private readonly DbService db;
    private readonly IBotStrings strings;
    private readonly ConcurrentHashSet<ulong> guildsEnabled;
    private readonly GuildSettingsService guildSettings;
    private readonly IServiceProvider services;
    private readonly BotConfigService botConfigService;

    public VerboseErrorsService(DiscordSocketClient client, DbService db, CommandHandler ch,
        IBotStrings strings,
        GuildSettingsService guildSettings,
        IServiceProvider services, BotConfigService botConfigService)
    {
        this.strings = strings;
        this.guildSettings = guildSettings;
        this.services = services;
        this.botConfigService = botConfigService;
        this.db = db;
        this.ch = ch;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        this.ch.CommandErrored += LogVerboseError;

        guildsEnabled = new ConcurrentHashSet<ulong>(gc
            .Where(x => x.VerboseErrors)
            .Select(x => x.GuildId));
    }

    public Task Unload()
    {
        ch.CommandErrored -= LogVerboseError;
        return Task.CompletedTask;
    }

    private async Task LogVerboseError(CommandInfo cmd, ITextChannel? channel, string reason, IUser user)
    {
        if (channel == null || !guildsEnabled.Contains(channel.GuildId))
            return;
        var perms = services.GetService<PermissionService>();
        var pc = await perms.GetCacheFor(channel.GuildId);
        foreach (var i in cmd.Aliases)
        {
            if (!(pc.Permissions != null
                  && pc.Permissions.CheckPermissions(new MewdekoUserMessage
                      {
                          Author = user, Channel = channel
                      },
                      i,
                      cmd.MethodName(), out _)))
                return;
        }

        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("Command Error")
                .WithDescription(reason)
                .AddField("Usages",
                    string.Join("\n", cmd.RealRemarksArr(strings, channel.Guild.Id, await guildSettings.GetPrefix(channel.Guild))))
                .WithFooter($"Run {await guildSettings.GetPrefix(channel.Guild.Id)}ve to disable these prompts.")
                .WithErrorColor();

            if (!botConfigService.Data.ShowInviteButton)
                await channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
            else
                await channel.SendMessageAsync(embed: embed.Build(), components: new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link, url: botConfigService.Data.SupportServer).Build()).ConfigureAwait(false);
        }
        catch
        {
            //ignore
        }
    }

    public async Task<bool> ToggleVerboseErrors(ulong guildId, bool? enabled = null)
    {
        await using (var uow = db.GetDbContext())
        {
            var gc = await uow.ForGuildId(guildId, set => set);

            if (enabled == null)
                enabled = gc.VerboseErrors = !gc.VerboseErrors; // Old behaviour, now behind a condition
            else gc.VerboseErrors = (bool)enabled; // New behaviour, just set it.

            await uow.SaveChangesAsync();
        }

        if ((bool)enabled) // This doesn't need to be duplicated inside the using block
            guildsEnabled.Add(guildId);
        else
            guildsEnabled.TryRemove(guildId);

        return (bool)enabled;
    }
}