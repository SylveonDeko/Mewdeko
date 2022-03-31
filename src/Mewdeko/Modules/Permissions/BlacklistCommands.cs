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
    [Group]
    public class BlacklistCommands : MewdekoSubmodule<BlacklistService>
    {
        private readonly IBotCredentials _creds;

        public BlacklistCommands(IBotCredentials creds) => _creds = creds;

        [MewdekoCommand, Usage, Description, Aliases, OwnerOnly]
        public Task UserBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.User);

        [MewdekoCommand, Usage, Description, Aliases, OwnerOnly]
        public Task UserBlacklist(AddRemove action, IUser usr) => Blacklist(action, usr.Id, BlacklistType.User);

        [MewdekoCommand, Usage, Description, Aliases, OwnerOnly]
        public Task ChannelBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Channel);

        [MewdekoCommand, Usage, Description, Aliases, OwnerOnly]
        public Task ServerBlacklist(AddRemove action, ulong id) => Blacklist(action, id, BlacklistType.Server);

        [MewdekoCommand, Usage, Description, Aliases, OwnerOnly]
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