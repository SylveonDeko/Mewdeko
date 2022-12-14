using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group, OwnerOnly]
    public class BlacklistCommands : MewdekoSubmodule<BlacklistService>
    {
        private readonly IBotCredentials creds;

        public BlacklistCommands(IBotCredentials creds) => this.creds = creds;

        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, ulong id, [Remainder] string? reason) => Blacklist(action, id, BlacklistType.User, reason);

        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, IUser usr, [Remainder] string? reason) => Blacklist(action, usr.Id, BlacklistType.User, reason);

        [Cmd, Aliases]
        public Task ChannelBlacklist(AddRemove action, ulong id, [Remainder] string? reason) => Blacklist(action, id, BlacklistType.Channel, reason);

        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, ulong id, [Remainder] string? reason) => Blacklist(action, id, BlacklistType.Server, reason);

        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, IGuild guild, [Remainder] string? reason) => Blacklist(action, guild.Id, BlacklistType.Server, reason);

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