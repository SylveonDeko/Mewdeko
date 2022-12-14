using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Gambling;

public partial class Gambling
{
    [Group]
    public class SlotCommands : GamblingSubmodule<GamblingService>
    {
        private static long totalBet;
        private static long totalPaidOut;

        private static readonly HashSet<ulong> RunningUsers = new();
        private readonly ICurrencyService cs;

        //here is a payout chart
        //https://lh6.googleusercontent.com/-i1hjAJy_kN4/UswKxmhrbPI/AAAAAAAAB1U/82wq_4ZZc-Y/DE6B0895-6FC1-48BE-AC4F-14D1B91AB75B.jpg
        //thanks to judge for helping me with this

        private readonly IImageCache images;

        public SlotCommands(IDataCache data, ICurrencyService cs, GamblingConfigService gamb) : base(gamb)
        {
            images = data.LocalImages;
            this.cs = cs;
        }

        [Cmd, Aliases, OwnerOnly]
        public async Task SlotStats()
        {
            //i remembered to not be a moron
            var paid = totalPaidOut;
            var bet = totalBet;

            if (bet <= 0)
                bet = 1;

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Slot Stats")
                .AddField(efb => efb.WithName("Total Bet").WithValue(bet.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("Paid Out").WithValue(paid.ToString()).WithIsInline(true))
                .WithFooter(efb => efb.WithText($"Payout Rate: {paid * 1.0 / bet * 100:f4}%"));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [Cmd, Aliases, OwnerOnly]
        public async Task SlotTest(int tests = 1000)
        {
            if (tests <= 0)
                return;
            //multi vs how many times it occured
            var dict = new Dictionary<int, int>();
            for (var i = 0; i < tests; i++)
            {
                var res = SlotMachine.Pull();
                if (dict.TryGetValue(res.Multiplier, out _))
                    dict[res.Multiplier]++;
                else
                    dict.Add(res.Multiplier, 1);
            }

            var sb = new StringBuilder();
            const int bet = 1;
            var payout = 0;
            foreach (var key in dict.Keys.OrderByDescending(x => x))
            {
                sb.AppendLine($"x{key} occured {dict[key]} times. {dict[key] * 1.0f / tests * 100}%");
                payout += key * dict[key];
            }

            await ctx.Channel.SendConfirmAsync("Slot Test Results", sb.ToString(),
                    footer: $"Total Bet: {tests * bet} | Payout: {payout * bet} | {payout * 1.0f / tests * 100}%")
                .ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task Slot(ShmartNumber amount)
        {
            if (!RunningUsers.Add(ctx.User.Id))
                return;
            try
            {
                if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                    return;
                const int maxAmount = 9999;
                if (amount > maxAmount)
                {
                    await ReplyErrorLocalizedAsync("max_bet_limit", maxAmount + CurrencySign).ConfigureAwait(false);
                    return;
                }

                if (!await cs.RemoveAsync(ctx.User, "Slot Machine", amount, false, true).ConfigureAwait(false))
                {
                    await ReplyErrorLocalizedAsync("not_enough", CurrencySign).ConfigureAwait(false);
                    return;
                }

                Interlocked.Add(ref totalBet, amount.Value);
                using var bgImage = Image.Load<Rgba32>(images.SlotBackground, out var format);
                var result = SlotMachine.Pull();
                var numbers = result.Numbers;

                for (var i = 0; i < 3; i++)
                {
                    using var randomImage = Image.Load(images.SlotEmojis[numbers[i]]);
                    bgImage.Mutate(x =>
                        x.DrawImage(randomImage, new Point(95 + (142 * i), 330), new GraphicsOptions()));
                }

                var printWon = amount * result.Multiplier;
                var n = 0;
                do
                {
                    var digit = (int)(printWon % 10);
                    using (var img = Image.Load(images.SlotEmojis[digit]))
                    {
                        bgImage.Mutate(x =>
                            x.DrawImage(img, new Point(230 - (n * 16), 462), new GraphicsOptions()));
                    }

                    n++;
                } while ((printWon /= 10) != 0);

                var printAmount = amount;
                n = 0;
                do
                {
                    var digit = (int)(printAmount % 10);
                    using (var img = Image.Load(images.SlotEmojis[numbers[digit]]))
                    {
                        bgImage.Mutate(x => x.DrawImage(img, new Point(148 + (105 * digit), 217), 1f));
                    }

                    n++;
                } while ((printAmount /= 10) != 0);

                var msg = GetText("better_luck");
                if (result.Multiplier != 0)
                {
                    await cs.AddAsync(ctx.User, $"Slot Machine x{result.Multiplier}",
                        amount * result.Multiplier, false, true).ConfigureAwait(false);
                    Interlocked.Add(ref totalPaidOut, amount * result.Multiplier);
                    if (result.Multiplier == 1)
                        msg = GetText("slot_single", CurrencySign, 1);
                    else if (result.Multiplier == 4)
                        msg = GetText("slot_two", CurrencySign, 4);
                    else if (result.Multiplier == 10)
                        msg = GetText("slot_three", 10);
                    else if (result.Multiplier == 30)
                        msg = GetText("slot_jackpot", 30);
                }

                await using var imgStream = bgImage?.ToStream(format);
                await ctx.Channel.SendFileAsync(imgStream, "result.png",
                        $"{ctx.User.Mention} {msg}\n`{GetText("slot_bet")}:`{amount} `{GetText("won")}:` {amount * result.Multiplier}{CurrencySign}")
                    .ConfigureAwait(false);
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    RunningUsers.Remove(ctx.User.Id);
                });
            }
        }

        public sealed class SlotMachine
        {
            public const int MaxValue = 5;

            private static readonly List<Func<int[], int>> WinningCombos = new()
            {
                //three flowers
                arr => arr.All(a => a == MaxValue) ? 30 : 0,
                //three of the same
                arr => !arr.Any(a => a != arr[0]) ? 10 : 0,
                //two flowers
                arr => arr.Count(a => a == MaxValue) == 2 ? 4 : 0,
                //one flower
                arr => arr.Any(a => a == MaxValue) ? 1 : 0
            };

            public static SlotResult Pull()
            {
                var numbers = new int[3];
                for (var i = 0; i < numbers.Length; i++) numbers[i] = new MewdekoRandom().Next(0, MaxValue + 1);
                var multi = 0;
                foreach (var t in WinningCombos)
                {
                    multi = t(numbers);
                    if (multi != 0)
                        break;
                }

                return new SlotResult(numbers, multi);
            }

            public struct SlotResult
            {
                public int[] Numbers { get; }
                public int Multiplier { get; }

                public SlotResult(int[] nums, int multi)
                {
                    Numbers = nums;
                    Multiplier = multi;
                }
            }
        }
    }
}