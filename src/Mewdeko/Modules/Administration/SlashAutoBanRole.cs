using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashAdministration
{
    /// <summary>
    ///     Slash command group for managing the AutoBanRole feature.
    /// </summary>
    [Group("autobanrole", "Allows you to set or remove a role from autobanning a user when they add it.")]
    public class SlashAutoBanRole : MewdekoSlashSubmodule<AutoBanRoleService>
    {
        /// <summary>
        ///     Lists all roles in the AutoBanRole list for the current guild.
        /// </summary>
        [SlashCommand("list", "List all roles that trigger auto-ban")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleList()
        {
            var rolesWithReasons = await Service.GetAutoBanRolesWithReasons(Context.Guild.Id);

            if (rolesWithReasons.Count == 0)
            {
                await ReplyErrorAsync(Strings.AbroleNone(Context.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roleList = new List<string>();
            foreach (var (roleId, reason) in rolesWithReasons)
            {
                var role = Context.Guild.GetRole(roleId);
                var roleDisplay = role != null ? $"{role.Mention} (`{role.Id}`)" : $"Deleted Role (`{roleId}`)";
                var reasonDisplay = !string.IsNullOrWhiteSpace(reason)
                    ? $"\n  └ {Strings.AbroleReasonDisplay(Context.Guild.Id, reason)}"
                    : "";
                roleList.Add($"• {roleDisplay}{reasonDisplay}");
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.AutoBanRolesTitle(Context.Guild.Id))
                .WithDescription(string.Join("\n", roleList))
                .WithColor(Mewdeko.ErrorColor)
                .WithFooter($"Total: {rolesWithReasons.Count} role(s)")
                .Build();

            await RespondAsync(embed: embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to add</param>
        [SlashCommand("add", "Add a role to the list of AutoBanRoles")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleAdd(IRole role)
        {
            var success = await Service.AddAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleAdd(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleExists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a role from the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to remove</param>
        [SlashCommand("remove", "Remove a role from the list of AutoBanRoles")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleRemove(IRole role)
        {
            var success = await Service.RemoveAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleRemove(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleNotexists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets or views the ban reason for an AutoBanRole.
        /// </summary>
        /// <param name="role">The role to set/view the reason for</param>
        /// <param name="reason">The reason to show in the audit log (leave empty to view current, use 'clear' to remove)</param>
        [SlashCommand("reason", "Set or view the ban reason for an auto-ban role")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleReason(IRole role, string? reason = null)
        {
            // Check if this role is in the auto-ban list
            var roles = await Service.GetAutoBanRoles(Context.Guild.Id);
            if (!roles.Contains(role.Id))
            {
                await ReplyErrorAsync(Strings.AbroleNotexists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
                return;
            }

            // If no reason provided, show the current reason
            if (reason == null)
            {
                var currentReason = await Service.GetAutoBanRoleReason(Context.Guild.Id, role.Id);
                if (string.IsNullOrWhiteSpace(currentReason))
                {
                    await ReplyConfirmAsync(Strings.AbroleReasonNone(Context.Guild.Id, role.Mention))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.AbroleReasonCurrent(Context.Guild.Id, role.Mention, currentReason))
                        .ConfigureAwait(false);
                }

                return;
            }

            // Clear reason if "clear" or empty
            var newReason = reason.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(reason)
                ? null
                : reason;

            var success = await Service.SetAutoBanRoleReason(Context.Guild.Id, role.Id, newReason);
            if (success)
            {
                if (newReason == null)
                {
                    await ReplyConfirmAsync(Strings.AbroleReasonCleared(Context.Guild.Id, role.Mention))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.AbroleReasonSet(Context.Guild.Id, role.Mention, newReason))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleNotexists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }
    }
}