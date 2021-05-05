using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Core.Common.TypeReaders.Models;
using System;
using System.Threading.Tasks;

namespace NadekoBot.Core.Common.TypeReaders
{
    public class StoopidTimeTypeReader : NadekoTypeReader<StoopidTime>
    {
        public StoopidTimeTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
        {
        }

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Task.FromResult(TypeReaderResult.FromError(CommandError.Unsuccessful, "Input is empty."));
            try
            {
                var time = StoopidTime.FromInput(input);
                return Task.FromResult(TypeReaderResult.FromSuccess(time));
            }
            catch (Exception ex)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.Exception, ex.Message));
            }
        }
    }
}
