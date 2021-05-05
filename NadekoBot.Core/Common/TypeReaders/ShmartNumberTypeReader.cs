using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NadekoBot.Core.Services;
using NLog;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NadekoBot.Core.Common.TypeReaders
{
    public class ShmartNumberTypeReader : NadekoTypeReader<ShmartNumber>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public ShmartNumberTypeReader(DiscordSocketClient client, CommandService cmds) : base(client, cmds)
        {
        }

        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            await Task.Yield();

            if (string.IsNullOrWhiteSpace(input))
                return TypeReaderResult.FromError(CommandError.ParseFailed, "Input is empty.");

            var i = input.Trim().ToUpperInvariant();

            i = i.Replace("K", "000");

            //can't add m because it will conflict with max atm

            if (TryHandlePercentage(services, context, i, out var num))
                return TypeReaderResult.FromSuccess(new ShmartNumber(num, i));
            try
            {
                var expr = new NCalc.Expression(i, NCalc.EvaluateOptions.IgnoreCase);
                expr.EvaluateParameter += (str, ev) => EvaluateParam(str, ev, context, services);
                var lon = (long)(decimal.Parse(expr.Evaluate().ToString()));
                return TypeReaderResult.FromSuccess(new ShmartNumber(lon, input));
            }
            catch (Exception ex)
            {
                _log.Info(ex);
                return TypeReaderResult.FromError(CommandError.ParseFailed, "Invalid input");
            }
        }

        private static void EvaluateParam(string name, NCalc.ParameterArgs args, ICommandContext ctx, IServiceProvider svc)
        {
            switch (name.ToUpperInvariant())
            {
                case "PI":
                    args.Result = Math.PI;
                    break;
                case "E":
                    args.Result = Math.E;
                    break;
                case "ALL":
                case "ALLIN":
                    args.Result = Cur(svc, ctx);
                    break;
                case "HALF":
                    args.Result = Cur(svc, ctx) / 2;
                    break;
                case "MAX":
                    args.Result = Max(svc, ctx);
                    break;
                default:
                    break;
            }
        }

        private static readonly Regex percentRegex = new Regex(@"^((?<num>100|\d{1,2})%)$", RegexOptions.Compiled);

        private static long Cur(IServiceProvider services, ICommandContext ctx)
        {
            var _db = services.GetService<DbService>();
            long cur;
            using (var uow = _db.GetDbContext())
            {
                cur = uow.DiscordUsers.GetUserCurrency(ctx.User.Id);
                uow.SaveChanges();
            }
            return cur;
        }

        private static long Max(IServiceProvider services, ICommandContext ctx)
        {
            var _bc = services.GetService<IBotConfigProvider>();
            var max = _bc.BotConfig.MaxBet;
            return max == 0
                ? Cur(services, ctx)
                : max;
        }

        private static bool TryHandlePercentage(IServiceProvider services, ICommandContext ctx, string input, out long num)
        {
            num = 0;
            var m = percentRegex.Match(input);
            if (m.Captures.Count != 0)
            {
                if (!long.TryParse(m.Groups["num"].ToString(), out var percent))
                    return false;

                num = (long)(Cur(services, ctx) * (percent / 100.0f));
                return true;
            }
            return false;
        }
    }
}
