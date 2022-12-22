using System.Threading.Tasks;
using CommandLine;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using MoreLinq;

namespace Mewdeko.Modules.Help.Services;

public class HelpService : ILateExecutor, INService
{
    private readonly BotConfigService bss;
    private readonly DiscordSocketClient client;
    private readonly DiscordPermOverrideService dpos;
    private readonly Mewdeko bot;
    private readonly BlacklistService blacklistService;
    private readonly IBotStrings strings;
    private readonly CommandService cmds;
    private readonly GlobalPermissionService perms;
    private readonly PermissionService nPerms;
    private readonly InteractionService interactionService;
    private readonly GuildSettingsService guildSettings;


    public HelpService(
        IBotStrings strings,
        DiscordPermOverrideService dpos,
        BotConfigService bss,
        DiscordSocketClient client,
        Mewdeko bot,
        BlacklistService blacklistService,
        CommandService cmds,
        GlobalPermissionService perms,
        PermissionService nPerms,
        InteractionService interactionService,
        GuildSettingsService guildSettings)
    {
        this.dpos = dpos;
        this.strings = strings;
        this.client = client;
        this.bot = bot;
        this.blacklistService = blacklistService;
        this.cmds = cmds;
        this.bss = bss;
        this.client.MessageReceived += HandlePing;
        this.client.JoinedGuild += HandleJoin;
        this.perms = perms;
        this.nPerms = nPerms;
        this.interactionService = interactionService;
        this.guildSettings = guildSettings;
    }

    public ComponentBuilder GetHelpComponents(IGuild? guild, IUser user, bool descriptions = true)
    {
        var modules = cmds.Commands.Select(x => x.Module).Where(x => !x.IsSubmodule).Distinct();
        var compBuilder = new ComponentBuilder();
        var selMenu = new SelectMenuBuilder().WithCustomId("helpselect");
        foreach (var i in modules.Where(x => !x.Attributes.Any(attribute => attribute is HelpDisabled)))
        {
            selMenu.Options.Add(new SelectMenuOptionBuilder()
                .WithLabel(i.Name).WithDescription(GetText($"module_description_{i.Name.ToLower()}", guild)).WithValue(i.Name.ToLower()));
        }

        compBuilder.WithButton("Toggle Descriptions", $"toggle-descriptions:{descriptions},{user.Id}");
        compBuilder.WithButton("Invite Me!", style: ButtonStyle.Link,
            url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands");
        compBuilder.WithButton("Donate to keep the bot running!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko");
        compBuilder.WithSelectMenu(selMenu);
        return compBuilder;
    }

    public async Task<EmbedBuilder> GetHelpEmbed(bool description, IGuild? guild, IMessageChannel channel, IUser user)
    {
        EmbedBuilder embed = new();
        embed.WithTitle($"{client.CurrentUser.Username} Help");
        embed.WithOkColor();
        embed.WithDescription(
            $"\nDo `{await guildSettings.GetPrefix(guild?.Id)}help command` to see a description of a command you need more info on!" +
            $"\nDo `{await guildSettings.GetPrefix(guild?.Id)}cmds category` to see the commands in that module." +
            "\n\n**Youtube Tutorials**\nhttps://www.youtube.com/channel/UCKJEaaZMJQq6lH33L3b_sTg\n\n**Links**\n" +
            $"[Documentation](https://mewdeko.tech) | [Support Server](https://discord.gg/mewdeko) | [Invite Me](https://discord.com/oauth2/authorize?client_id={bot.Client.CurrentUser.Id}&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Donate!](https://ko-fi.com/mewdeko)");
        var modules = cmds.Commands.Select(x => x.Module).Where(x => !x.IsSubmodule && !x.Attributes.Any(attribute => attribute is HelpDisabled)).Distinct();
        var count = 0;
        if (description)
        {
            foreach (var mod in modules)
            {
                embed.AddField($"{await CheckEnabled(guild?.Id, channel, user, mod.Name)} {mod.Name}", $">>> {GetModuleDescription(mod.Name, guild)}", true);
            }
        }
        else
        {
            foreach (var i in modules.Batch(modules.Count() / 2))
            {
                embed.AddField(count == 0 ? "Categories" : "_ _",
                    string.Join("\n", i.Select(x => $"> {CheckEnabled(guild?.Id, channel, user, x.Name).GetAwaiter().GetResult()} {Format.Bold(x.Name)}")), true);
                count++;
            }
        }

        return embed;
    }

    public async Task<string> CheckEnabled(ulong? guildId, IMessageChannel channel, IUser user, string moduleName)
    {
        if (!guildId.HasValue || guildId is null)
            return "✅";
        var pc = await nPerms.GetCacheFor(guildId.Value);
        if (perms.BlockedModules.Contains(moduleName.ToLower())) return "🌐❌";
        return !pc.Permissions.CheckSlashPermissions(moduleName, "none", user, channel, out _) ? "❌" : "✅";
    }

    public string? GetModuleDescription(string module, IGuild? guild) => GetText($"module_description_{module.ToLower()}", guild);

    public Task LateExecute(DiscordSocketClient discordSocketClient, IGuild? guild, IUserMessage msg)
    {
        var settings = bss.Data;
        if (guild != null) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(settings.DmHelpText) || settings.DmHelpText == "-")
            return Task.CompletedTask;

        return SmartEmbed.TryParse(settings.DmHelpText, null, out var embed, out var plainText, out var components)
            ? msg.Channel.SendMessageAsync(plainText, embeds: embed, components: components.Build())
            : msg.Channel.SendMessageAsync(settings.DmHelpText);
    }

    private Task HandlePing(SocketMessage msg)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Content == $"<@{client.CurrentUser.Id}>" || msg.Content == $"<@!{client.CurrentUser.Id}>")
            {
                if (msg.Channel is ITextChannel chan)
                {
                    var eb = new EmbedBuilder();
                    eb.WithOkColor();
                    eb.WithDescription(
                        $"Hi there! To see my command categories do `{await guildSettings.GetPrefix(chan.Guild)}cmds`\nMy current Prefix is `{await guildSettings.GetPrefix(chan.Guild)}`\nIf you need help using the bot feel free to join the [Support Server](https://discord.gg/mewdeko)!\n**Please support me! While this bot is free it's not free to run! https://ko-fi.com/mewdeko**\n\n I hope you have a great day!");
                    eb.WithThumbnailUrl("https://cdn.discordapp.com/emojis/914307922287276052.gif");
                    eb.WithFooter(new EmbedFooterBuilder().WithText(client.CurrentUser.Username).WithIconUrl(client.CurrentUser.RealAvatarUrl().ToString()));
                    await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                }
            }
        });
        return Task.CompletedTask;
    }

    public Task HandleJoin(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(guild.Id))
                return;

            var e = guild.DefaultChannel;
            var px = await guildSettings.GetPrefix(guild);
            var eb = new EmbedBuilder
            {
                Description =
                    $"Hi, thanks for inviting Mewdeko! I hope you like the bot, and discover all its features! The default prefix is `{px}.` This can be changed with the prefix command."
            };
            eb.AddField("How to look for commands",
                $"1) Use the {px}cmds command to see all the categories\n2) use {px}cmds with the category name to glance at what commands it has. ex: `{px}cmds mod`\n3) Use {px}h with a command name to view its help. ex: `{px}h purge`");
            eb.AddField("Have any questions, or need my invite link?", "Support Server: https://discord.gg/mewdeko \nInvite Link: https://mewdeko.tech/invite");
            eb.AddField("Youtube Channel", "https://youtube.com/channel/UCKJEaaZMJQq6lH33L3b_sTg");
            eb.WithThumbnailUrl(
                "https://cdn.discordapp.com/emojis/968564817784877066.gif");
            eb.WithOkColor();
            await e.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    public async Task<EmbedBuilder> GetCommandHelp(CommandInfo com, IGuild guild)
    {
        if (com.Attributes.Any(x => x is HelpDisabled))
            return new EmbedBuilder().WithDescription("Help is disabled for this command.");
        var prefix = await guildSettings.GetPrefix(guild);
        var potentialCommand = interactionService.SlashCommands.FirstOrDefault(x => string.Equals(x.MethodName, com.MethodName(), StringComparison.CurrentCultureIgnoreCase));
        var str = $"**{prefix + com.Aliases[0]}**";
        var alias = com.Aliases.Skip(1).FirstOrDefault();
        if (alias != null)
            str += $" **| {prefix + alias}**";
        var em = new EmbedBuilder().AddField(fb =>
            fb.WithName(str).WithValue($"{com.RealSummary(strings, guild.Id, prefix)}").WithIsInline(true));

        var tryGetOverrides = dpos.TryGetOverrides(guild.Id, com.Name, out var overrides);
        var reqs = GetCommandRequirements(com, tryGetOverrides ? overrides : null);
        var botReqs = GetCommandBotRequirements(com);
        var attribute = (RatelimitAttribute)com.Preconditions.FirstOrDefault(x => x is RatelimitAttribute);
        if (reqs.Length > 0)
            em.AddField("User Permissions", string.Join("\n", reqs));
        if (botReqs.Length > 0)
            em.AddField("Bot Permissions", string.Join("\n", botReqs));
        if (attribute?.Seconds > 0)
        {
            em.AddField("Ratelimit", $"{attribute.Seconds} seconds");
        }

        if (potentialCommand is not null)
        {
            var globalCommands = await client.Rest.GetGlobalApplicationCommands();
            var guildCommands = await client.Rest.GetGuildApplicationCommands(guild.Id);
            var globalCommand = globalCommands.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);
            var guildCommand = guildCommands.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);
            if (globalCommand is not null)
                em.AddField("Slash Command", potentialCommand == null ? "`None`" : $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{globalCommand.Id}>");
            else if (guildCommand is not null)
                em.AddField("Slash Command", potentialCommand == null ? "`None`" : $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{guildCommand.Id}>");
        }

        em.AddField(fb => fb.WithName(GetText("usage", guild)).WithValue(string.Join("\n",
                    Array.ConvertAll(com.RealRemarksArr(strings, guild?.Id, prefix),
                        arg => Format.Code(arg))))
                .WithIsInline(false))
            .WithFooter($"Module: {com.Module.GetTopLevelModule().Name} || Submodule: {com.Module.Name.Replace("Commands", "")} || Method Name: {com.MethodName()}")
            .WithColor(Mewdeko.OkColor);

        var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is MewdekoOptionsAttribute))
            ?.OptionType;
        if (opt == null) return em;
        var hs = GetCommandOptionHelp(opt);
        if (!string.IsNullOrWhiteSpace(hs))
            em.AddField(GetText("options", guild), hs);

        return em;
    }

    public static string GetCommandOptionHelp(Type opt)
    {
        var strs = GetCommandOptionHelpList(opt);

        return string.Join("\n", strs);
    }

    public static List<string> GetCommandOptionHelpList(Type opt) =>
        opt.GetProperties()
            .Select(x => Array.Find(x.GetCustomAttributes(true), a => a is OptionAttribute))
            .Where(x => x != null).Cast<OptionAttribute>().Select(x =>
            {
                var toReturn = $"`--{x.LongName}`";

                if (!string.IsNullOrWhiteSpace(x.ShortName))
                    toReturn += $" (`-{x.ShortName}`)";

                toReturn += $"   {x.HelpText}  ";
                return toReturn;
            }).ToList();

    public static string[] GetCommandRequirements(CommandInfo cmd, GuildPermission? overrides = null)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var userPerm = (UserPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute);

        var userPermString = string.Empty;
        if (userPerm is not null)
        {
            if (userPerm.UserPermissionAttribute.ChannelPermission is { } cPerm)
                userPermString = GetPreconditionString(cPerm);
            if (userPerm.UserPermissionAttribute.GuildPermission is { } gPerm)
                userPermString = GetPreconditionString(gPerm);
        }

        if (overrides is null)
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(userPermString);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(Format.Strikethrough(userPermString));

            toReturn.Add(GetPreconditionString(overrides.Value));
        }

        return toReturn.ToArray();
    }

    public static string[] GetCommandBotRequirements(CommandInfo cmd)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var botPerm = (BotPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute);

        var botPermString = string.Empty;
        if (botPerm is not null)
        {
            if (botPerm.ChannelPermission is { } cPerm)
                botPermString = GetPreconditionString(cPerm);
            if (botPerm.GuildPermission is { } gPerm)
                botPermString = GetPreconditionString(gPerm);
        }

        if (!string.IsNullOrWhiteSpace(botPermString))
            toReturn.Add(botPermString);

        return toReturn.ToArray();
    }

    public static string GetPreconditionString(ChannelPermission perm) =>
        (perm + " Channel Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);

    public static string GetPreconditionString(GuildPermission perm) =>
        (perm + " Server Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);

    private string? GetText(string? text, IGuild? guild, params object?[] replacements) =>
        strings.GetText(text, guild?.Id, replacements);
}