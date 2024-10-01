using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    /// <summary>
    ///     Provides commands for resetting XP for users and the entire guild.
    /// </summary>
    public class ResetCommands : MewdekoSubmodule<XpService>
    {
        /// <summary>
        ///     Resets the XP of a specific user within the guild.
        /// </summary>
        /// <param name="user">The guild user whose XP will be reset.</param>
        /// <returns>A task that represents the asynchronous operation of resetting XP for a specific user.</returns>
        /// <remarks>
        ///     This command resets the XP of the specified user in the guild to zero.
        ///     It requires the command invoker to have administrative permissions within the guild.
        ///     The user is prompted for confirmation before the reset is executed.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task XpReset(IGuildUser user)
        {
            return XpReset(user.Id);
        }

        /// <summary>
        ///     Resets the XP of a user specified by their user ID within the guild.
        /// </summary>
        /// <param name="userId">The ID of the user whose XP will be reset.</param>
        /// <returns>A task that represents the asynchronous operation of resetting XP for a user identified by their ID.</returns>
        /// <remarks>
        ///     This command allows for XP reset based on the user ID.
        ///     It requires the command invoker to have administrative permissions.
        ///     A confirmation prompt is presented before proceeding with the reset.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task XpReset(ulong userId)
        {
            var embed = new EmbedBuilder()
                .WithTitle(GetText("reset"))
                .WithDescription(GetText("reset_user_confirm"));

            if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                return;

            Service.XpReset(ctx.Guild.Id, userId);

            await ReplyConfirmLocalizedAsync("reset_user", userId).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets the XP for all users within the guild.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of resetting XP for all users in the guild.</returns>
        /// <remarks>
        ///     This command initiates a complete XP reset for all users in the guild.
        ///     It is protected by an administrative permission check and requires user confirmation before execution.
        ///     Upon successful reset, a confirmation message is sent to the invoking user.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task XpReset()
        {
            var embed = new EmbedBuilder()
                .WithTitle(GetText("reset"))
                .WithDescription(GetText("reset_server_confirm"));

            if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                return;

            Service.XpReset(ctx.Guild.Id);

            await ReplyConfirmLocalizedAsync("reset_server").ConfigureAwait(false);
        }
    }
}