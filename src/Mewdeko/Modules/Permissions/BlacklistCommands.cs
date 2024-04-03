using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

/// <summary>
/// Defines permission-related commands, including user, channel, and server blacklist operations.
/// </summary>
public partial class Permissions
{
    /// <summary>
    /// Represents owner only blacklist commands for managing user, channel, and server blacklists.
    /// </summary>
    /// <param name="creds">Bot credentials</param>
    [Group, OwnerOnly]
    public class BlacklistCommands(IBotCredentials creds) : MewdekoSubmodule<BlacklistService>
    {
        /// <summary>
        /// Blacklists or unblacklists a user by their ID, with an optional reason.
        /// </summary>
        /// <param name="action">Specifies whether to add or remove from the blacklist.</param>
        /// <param name="id">The user's ID.</param>
        /// <param name="reason">The reason for the blacklist operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist add user 123456789012345678 Reason for blacklisting
        /// .blacklist rem user 123456789012345678
        /// </example>
        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, ulong id, [Remainder] string? reason) =>
            Blacklist(action, id, BlacklistType.User, reason);

        /// <summary>
        /// Blacklists or unblacklists a user by their user object, with an optional reason.
        /// </summary>
        /// <param name="action">Specifies whether to add or remove from the blacklist.</param>
        /// <param name="usr">The user object.</param>
        /// <param name="reason">The reason for the blacklist operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist add user @User Reason for blacklisting
        /// .blacklist rem user @User
        /// </example>
        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, IUser usr, [Remainder] string? reason) =>
            Blacklist(action, usr.Id, BlacklistType.User, reason);

        /// <summary>
        /// Blacklists or unblacklists a channel by its ID, with an optional reason.
        /// </summary>
        /// <param name="action">Specifies whether to add or remove from the blacklist.</param>
        /// <param name="id">The channel's ID.</param>
        /// <param name="reason">The reason for the blacklist operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist add channel 123456789012345678 Reason for blacklisting
        /// .blacklist rem channel 123456789012345678
        /// </example>
        [Cmd, Aliases]
        public Task ChannelBlacklist(AddRemove action, ulong id, [Remainder] string? reason) =>
            Blacklist(action, id, BlacklistType.Channel, reason);

        /// <summary>
        /// Blacklists or unblacklists a server by its ID, with an optional reason.
        /// </summary>
        /// <param name="action">Specifies whether to add or remove from the blacklist.</param>
        /// <param name="id">The server's ID.</param>
        /// <param name="reason">The reason for the blacklist operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist add server 123456789012345678 Reason for blacklisting
        /// .blacklist rem server 123456789012345678
        /// </example>
        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, ulong id, [Remainder] string? reason) =>
            Blacklist(action, id, BlacklistType.Server, reason);

        /// <summary>
        /// Blacklists or unblacklists a server by its guild object, with an optional reason.
        /// </summary>
        /// <param name="action">Specifies whether to add or remove from the blacklist.</param>
        /// <param name="guild">The guild object.</param>
        /// <param name="reason">The reason for the blacklist operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist add server @Server Reason for blacklisting
        /// .blacklist rem server @Server
        /// </example>
        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, IGuild guild, [Remainder] string? reason) =>
            Blacklist(action, guild.Id, BlacklistType.Server, reason);

        /// <summary>
        /// Performs a manual check for blacklisted entities across the bot's scope.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <example>
        /// .blacklist check
        /// </example>
        [Cmd, Aliases]
        public async Task ManualBlacklistCheck()
        {
            await ctx.Channel.SendConfirmAsync("Sending manual check...");
            await Service.SendManualCheck();
        }

        private async Task Blacklist(AddRemove action, ulong id, BlacklistType type, string? reason)
        {
            switch (action)
            {
                case AddRemove.Add when creds.OwnerIds.Contains(id):
                    return;
                case AddRemove.Add:
                    Service.Blacklist(type, id, reason);
                    break;
                case AddRemove.Rem:
                default:
                    Service.UnBlacklist(type, id);
                    break;
            }

            if (action == AddRemove.Add)
            {
                await ReplyConfirmLocalizedAsync("blacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("unblacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
            }
        }
    }
}