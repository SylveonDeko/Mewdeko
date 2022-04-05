using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group, OwnerOnly]
    public class BlacklistCommands : MewdekoSubmodule<BlacklistService>
    {
        private readonly IBotCredentials _creds;

        public BlacklistCommands(IBotCredentials creds) => _creds = creds;

        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.User);

        [Cmd, Aliases]
        public Task UserBlacklist(AddRemove action, IUser usr) => Blacklist(action, usr.Id, BlacklistType.User);

        [Cmd, Aliases]
        public Task ChannelBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Channel);

        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Server);

        [Cmd, Aliases]
        public Task ServerBlacklist(AddRemove action, IGuild guild) => Blacklist(action, guild.Id, BlacklistType.Server);

        private async Task Blacklist(AddRemove action, ulong id, BlacklistType type)
        {
            switch (action)
            {
                case AddRemove.Add when _creds.OwnerIds.Contains(id):
                    return;
                case AddRemove.Add:
                    Service.Blacklist(type, id);
                    break;
                case AddRemove.Rem:
                default:
                    Service.UnBlacklist(type, id);
                    break;
            }

            if (action == AddRemove.Add)
                await ReplyConfirmLocalizedAsync("blacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("unblacklisted", Format.Code(type.ToString()),
                    Format.Code(id.ToString())).ConfigureAwait(false);
        }
    }
}