using Discord.Interactions;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Slash commands for managing role monitoring settings, blacklists, and whitelists.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
[Group("rolemonitor", "Monitor and punish dangerous role or permission grants")]
public class SlashRoleMonitor(IDataConnectionFactory dbFactory) : MewdekoSlashModuleBase<RoleMonitorService>
{
    private async Task<bool> IsServerOwner()
    {
        if (ctx.User.Id == ctx.Guild.OwnerId) return true;
        await ctx.Interaction.SendEphemeralErrorAsync(Strings.ServerOwnerOnly(ctx.Guild.Id), Config)
            .ConfigureAwait(false);
        return false;
    }

    private async Task<GuildPermission?> ParsePermission(string permission)
    {
        if (Enum.TryParse<GuildPermission>(permission, true, out var parsed)) return parsed;
        await ErrorAsync(Strings.InvalidGuildPermission(ctx.Guild.Id, permission)).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    ///     Sets the default punishment action for the guild.
    /// </summary>
    /// <param name="punishmentAction">The default punishment action to set.</param>
    [SlashCommand("default-punishment", "Sets the default punishment for role monitor violations")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetDefaultPunishment(
        [Summary("punishment", "The punishment to apply")]
        PunishmentAction punishmentAction)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await Service.SetDefaultPunishmentAsync(ctx.Guild, punishmentAction).ConfigureAwait(false);
        await ConfirmAsync(Strings.DefaultPunishmentSet(ctx.Guild.Id, punishmentAction)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a role to the blacklist.
    /// </summary>
    /// <param name="role">The role to blacklist.</param>
    /// <param name="punishmentAction">Optional punishment action specific to this role.</param>
    [SlashCommand("blacklist-role-add", "Blacklists a role so granting it is punished")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddBlacklistedRole([Summary("role", "The role to blacklist")] IRole role,
        [Summary("punishment", "Punishment specific to this role")]
        PunishmentAction? punishmentAction = null)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        try
        {
            await Service.AddBlacklistedRoleAsync(ctx.Guild, role, punishmentAction).ConfigureAwait(false);
            await ConfirmAsync(Strings.RoleBlacklisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await ErrorAsync(Strings.RoleBlacklistError(ctx.Guild.Id, ex.Message, role.Mention))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a role from the blacklist.
    /// </summary>
    /// <param name="role">The role to remove from the blacklist.</param>
    [SlashCommand("blacklist-role-remove", "Removes a role from the blacklist")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveBlacklistedRole([Summary("role", "The role to unblacklist")] IRole role)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        try
        {
            await Service.RemoveBlacklistedRoleAsync(ctx.Guild, role).ConfigureAwait(false);
            await ConfirmAsync(Strings.RoleUnblacklisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await ErrorAsync(Strings.RoleUnblacklistError(ctx.Guild.Id, ex.Message, role.Mention))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a permission to the blacklist.
    /// </summary>
    /// <param name="permission">The permission to blacklist.</param>
    /// <param name="punishmentAction">Optional punishment action specific to this permission.</param>
    [SlashCommand("blacklist-permission-add", "Blacklists a guild permission so granting it is punished")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddBlacklistedPermission(
        [Summary("permission", "The guild permission to blacklist")]
        [Autocomplete(typeof(GuildPermissionAutocompleter))]
        string permission,
        [Summary("punishment", "Punishment specific to this permission")]
        PunishmentAction? punishmentAction = null)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        var parsed = await ParsePermission(permission).ConfigureAwait(false);
        if (parsed is null) return;
        try
        {
            await Service.AddBlacklistedPermissionAsync(ctx.Guild, parsed.Value, punishmentAction)
                .ConfigureAwait(false);
            await ConfirmAsync(Strings.PermissionBlacklisted(ctx.Guild.Id, parsed.Value)).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await ErrorAsync(Strings.PermissionBlacklistError(ctx.Guild.Id, ex.Message, parsed.Value))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a permission from the blacklist.
    /// </summary>
    /// <param name="permission">The permission to remove from the blacklist.</param>
    [SlashCommand("blacklist-permission-remove", "Removes a guild permission from the blacklist")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveBlacklistedPermission(
        [Summary("permission", "The guild permission to unblacklist")]
        [Autocomplete(typeof(GuildPermissionAutocompleter))]
        string permission)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        var parsed = await ParsePermission(permission).ConfigureAwait(false);
        if (parsed is null) return;
        try
        {
            await Service.RemoveBlacklistedPermissionAsync(ctx.Guild, parsed.Value).ConfigureAwait(false);
            await ConfirmAsync(Strings.PermissionUnblacklisted(ctx.Guild.Id, parsed.Value)).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await ErrorAsync(Strings.PermissionUnblacklistError(ctx.Guild.Id, ex.Message, parsed.Value))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a user to the whitelist.
    /// </summary>
    /// <param name="user">The user to whitelist.</param>
    [SlashCommand("whitelist-user-add", "Exempts a user from role monitoring")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddWhitelistedUser([Summary("user", "The user to whitelist")] IGuildUser user)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await Service.AddWhitelistedUserAsync(ctx.Guild, user).ConfigureAwait(false);
        await ConfirmAsync(Strings.UserWhitelisted(ctx.Guild.Id, user.Username)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a user from the whitelist.
    /// </summary>
    /// <param name="user">The user to remove from the whitelist.</param>
    [SlashCommand("whitelist-user-remove", "Removes a user from the whitelist")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveWhitelistedUser([Summary("user", "The user to unwhitelist")] IGuildUser user)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await Service.RemoveWhitelistedUserAsync(ctx.Guild, user).ConfigureAwait(false);
        await ConfirmAsync(Strings.UserUnwhitelisted(ctx.Guild.Id, user.Username)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a role to the whitelist.
    /// </summary>
    /// <param name="role">The role to whitelist.</param>
    [SlashCommand("whitelist-role-add", "Exempts members of a role from role monitoring")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddWhitelistedRole([Summary("role", "The role to whitelist")] IRole role)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await Service.AddWhitelistedRoleAsync(ctx.Guild, role).ConfigureAwait(false);
        await ConfirmAsync(Strings.RoleWhitelisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a role from the whitelist.
    /// </summary>
    /// <param name="role">The role to remove from the whitelist.</param>
    [SlashCommand("whitelist-role-remove", "Removes a role from the whitelist")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveWhitelistedRole([Summary("role", "The role to unwhitelist")] IRole role)
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await Service.RemoveWhitelistedRoleAsync(ctx.Guild, role).ConfigureAwait(false);
        await ConfirmAsync(Strings.RoleUnwhitelisted(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all blacklisted and whitelisted roles, permissions and users.
    /// </summary>
    [SlashCommand("list", "Lists blacklisted roles and permissions and whitelisted users and roles")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ListBlacklists()
    {
        if (!await IsServerOwner().ConfigureAwait(false)) return;
        await DeferAsync().ConfigureAwait(false);
        await using var context = await dbFactory.CreateConnectionAsync();

        var blacklistedRoles = await context.BlacklistedRoles
            .Where(r => r.GuildId == ctx.Guild.Id)
            .ToListAsync();

        var blacklistedPermissions = await context.BlacklistedPermissions
            .Where(p => p.GuildId == ctx.Guild.Id)
            .ToListAsync();

        var whitelistedUsers = await context.WhitelistedUsers
            .Where(u => u.GuildId == ctx.Guild.Id)
            .ToListAsync();

        var whitelistedRoles = await context.WhitelistedRoles
            .Where(r => r.GuildId == ctx.Guild.Id)
            .ToListAsync();

        var embed = new EmbedBuilder()
            .WithTitle(Strings.BlacklistWhitelistListTitle(ctx.Guild.Id))
            .WithColor(Color.Red);

        if (blacklistedRoles.Any())
        {
            var roles = blacklistedRoles.Select(r =>
            {
                var role = ctx.Guild.GetRole(r.RoleId);
                var roleName = role != null ? role.Name : Strings.RoleIdFormat(ctx.Guild.Id, r.RoleId);
                var punishment = r.PunishmentAction?.ToString() ?? Strings.Default(ctx.Guild.Id);
                return $"{roleName}, {Strings.PunishmentFormat(ctx.Guild.Id, punishment)}";
            });

            embed.AddField(Strings.BlacklistedRoles(ctx.Guild.Id), string.Join("\n", roles));
        }
        else
        {
            embed.AddField(Strings.BlacklistedRoles(ctx.Guild.Id), Strings.NoBlacklistedRoles(ctx.Guild.Id));
        }

        if (blacklistedPermissions.Any())
        {
            var permissions = blacklistedPermissions.Select(p =>
            {
                var punishment = p.PunishmentAction?.ToString() ?? Strings.Default(ctx.Guild.Id);
                return $"{p.Permission}, {Strings.PunishmentFormat(ctx.Guild.Id, punishment)}";
            });

            embed.AddField(Strings.BlacklistedPermissions(ctx.Guild.Id), string.Join("\n", permissions));
        }
        else
        {
            embed.AddField(Strings.BlacklistedPermissions(ctx.Guild.Id),
                Strings.NoBlacklistedPermissions(ctx.Guild.Id));
        }

        if (whitelistedRoles.Any())
        {
            var roles = whitelistedRoles.Select(r =>
            {
                var role = ctx.Guild.GetRole(r.RoleId);
                return role != null ? role.Name : Strings.RoleIdFormat(ctx.Guild.Id, r.RoleId);
            });

            embed.AddField(Strings.WhitelistedRoles(ctx.Guild.Id), string.Join("\n", roles));
        }
        else
        {
            embed.AddField(Strings.WhitelistedRoles(ctx.Guild.Id), Strings.NoWhitelistedRoles(ctx.Guild.Id));
        }

        if (whitelistedUsers.Count != 0)
        {
            var users = new List<string>();
            foreach (var u in whitelistedUsers)
            {
                var user = await ctx.Guild.GetUserAsync(u.UserId).ConfigureAwait(false);
                users.Add(user != null ? user.Username : Strings.UserIdFormat(ctx.Guild.Id, u.UserId));
            }

            embed.AddField(Strings.WhitelistedUsers(ctx.Guild.Id), string.Join("\n", users));
        }
        else
        {
            embed.AddField(Strings.WhitelistedUsers(ctx.Guild.Id), Strings.NoWhitelistedUsers(ctx.Guild.Id));
        }

        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }
}