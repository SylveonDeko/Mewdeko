using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Core.Common.TypeReaders;

namespace Mewdeko.Common.TypeReaders
{
    /// <summary>
    ///     Used instead of bool for more flexible keywords for true/false only in the permission module
    /// </summary>
    public class PermValue : MewdekoTypeReader<PermValue>
    {
        public PermValue(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
        {
        }

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider _)
        {
            input = input.ToUpperInvariant();
            switch (input)
            {
                case "1":
                case "T":
                case "TRUE":
                case "ENABLE":
                case "ENABLED":
                case "ALLOW":
                case "PERMIT":
                case "UNBAN":
                    return Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Allow));
                case "0":
                case "F":
                case "FALSE":
                case "DENY":
                case "DISABLE":
                case "DISABLED":
                case "DISALLOW":
                case "BAN":
                    return Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Deny));
                case "2":
                case "N":
                case "Neutral":
                case "Inherit":
                    return Task.FromResult(TypeReaderResult.FromSuccess(Discord.PermValue.Inherit));
                default:
                    return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                        "Must be either deny or allow."));
            }
        }
    }
}