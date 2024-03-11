using System.IO;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Currency.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Currency;

public class Currency(InteractiveService interactive) : MewdekoModuleBase<ICurrencyService>
{
    [Cmd, Aliases]
    public async Task Cash()
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(
                $"Your current balance is: {await Service.GetUserBalanceAsync(Context.User.Id, Context.Guild.Id)} {await Service.GetCurrencyEmote(Context.Guild.Id)}");

        await ReplyAsync(embed: eb.Build());
    }

    [Cmd, Aliases]
    public async Task CoinFlip(long betAmount, string guess)
    {
        var currentBalance = await Service.GetUserBalanceAsync(Context.User.Id, Context.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync("Invalid bet amount!");
            return;
        }

        var coinFlip = new Random().Next(2) == 0 ? "heads" : "tails";
        if (coinFlip.Equals(guess, StringComparison.OrdinalIgnoreCase))
        {
            await Service.AddUserBalanceAsync(Context.User.Id, betAmount, Context.Guild.Id);
            await Service.AddTransactionAsync(Context.User.Id, betAmount, "Won Coin Flip", Context.Guild.Id);
            await ReplyAsync(
                $"It was {coinFlip}! You won {betAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}!");
        }
        else
        {
            await Service.AddUserBalanceAsync(Context.User.Id, -betAmount, Context.Guild.Id);
            await Service.AddTransactionAsync(Context.User.Id, -betAmount, "Lost Coin Flip", Context.Guild.Id);
            await ReplyAsync(
                $"It was {coinFlip}. You lost {betAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}.");
        }
    }

    [Cmd, Aliases]
    public async Task DailyReward()
    {
        var (rewardAmount, cooldownSeconds) = await Service.GetReward(Context.Guild.Id);
        if (rewardAmount == 0)
        {
            await Context.Channel.SendErrorAsync("Daily reward is not set up.");
            return;
        }

        var minimumTimeBetweenClaims = TimeSpan.FromSeconds(cooldownSeconds);

        var recentTransactions = (await Service.GetTransactionsAsync(Context.User.Id, Context.Guild.Id))
            .Where(t => t.Description == "Daily Reward" &&
                        t.DateAdded > DateTime.UtcNow - minimumTimeBetweenClaims);

        if (recentTransactions.Any())
        {
            var nextAllowedClaimTime = recentTransactions.Max(t => t.DateAdded) + minimumTimeBetweenClaims;

            await Context.Channel.SendErrorAsync(
                $"You already claimed your daily reward. Come back at {TimestampTag.FromDateTime(nextAllowedClaimTime.Value)}");
            return;
        }

        await Service.AddUserBalanceAsync(Context.User.Id, rewardAmount, Context.Guild.Id);
        await Service.AddTransactionAsync(Context.User.Id, rewardAmount, "Daily Reward", Context.Guild.Id);
        await Context.Channel.SendConfirmAsync(
            $"You claimed your daily reward of {rewardAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}!");
    }


    [Cmd, Aliases]
    public async Task HighLow(string guess)
    {
        var currentNumber = new Random().Next(1, 11);
        var nextNumber = new Random().Next(1, 11);

        if (guess.Equals("higher", StringComparison.OrdinalIgnoreCase) && nextNumber > currentNumber
            || guess.Equals("lower", StringComparison.OrdinalIgnoreCase) && nextNumber < currentNumber)
        {
            await Service.AddUserBalanceAsync(Context.User.Id, 100, Context.Guild.Id);
            await ReplyAsync(
                $"Previous number: {currentNumber}. Next number: {nextNumber}. You guessed right! You won 100 {await Service.GetCurrencyEmote(Context.Guild.Id)}!");
        }
        else
        {
            await Service.AddUserBalanceAsync(Context.User.Id, -100, Context.Guild.Id);
            await ReplyAsync(
                $"Previous number: {currentNumber}. Next number: {nextNumber}. You guessed wrong! You lost 100 {await Service.GetCurrencyEmote(Context.Guild.Id)}.");
        }
    }

    [Cmd, Aliases]
    public async Task Leaderboard()
    {
        var users = (await Service.GetAllUserBalancesAsync(Context.Guild.Id))
            .OrderByDescending(u => u.Balance)
            .ToList();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((users.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle("Leaderboard")
                .WithDescription($"Top {users.Count} users in {Context.Guild.Name}")
                .WithColor(Color.Blue);

            // Add the top 10 users for this page
            for (var i = index * 10; i < (index + 1) * 10 && i < users.Count; i++)
            {
                var user = await Context.Guild.GetUserAsync(users[i].UserId) ??
                           (IUser)await Context.Client.GetUserAsync(users[i].UserId);
                pageBuilder.AddField($"{i + 1}. {user.Username}",
                    $"{users[i].Balance} {await Service.GetCurrencyEmote(Context.Guild.Id)}", inline: true);
            }

            return pageBuilder;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task SetDaily(int amount, StoopidTime time)
    {
        await Service.SetReward(amount, time.Time.Seconds, Context.Guild.Id);
        await ctx.Channel.SendConfirmAsync(
            $"Daily reward set to {amount} {await Service.GetCurrencyEmote(Context.Guild.Id)} every {time.Time.Seconds} seconds.");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task SpinWheel(long betAmount = 0)
    {
        var balance = await Service.GetUserBalanceAsync(Context.User.Id, Context.Guild.Id);
        if (balance <= 0)
        {
            await ctx.Channel.SendErrorAsync(
                $"You either have no {Service.GetCurrencyEmote(Context.Guild.Id)} or are negative. Please do dailyreward and try again.");
            return;
        }

        if (betAmount > balance)
        {
            await ctx.Channel.SendErrorAsync(
                $"You don't have enough {Service.GetCurrencyEmote(Context.Guild.Id)} to place that bet.");
            return;
        }

        string[] segments =
        [
            "-$10", "-10%", "+$10", "+30%", "+$30", "-5%"
        ];
        int[] weights =
        [
            2, 2, 1, 1, 1, 2
        ];
        var rand = new Random();
        var winningSegment = GenerateWeightedRandomSegment(segments.Length, weights, rand);

        // Prepare the wheel image
        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        DrawWheel(canvas, segments.Length, segments, winningSegment + 2); // Adjust the index as needed

        using var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Seek(0, SeekOrigin.Begin);

        var balanceChange = await ComputeBalanceChange(segments[winningSegment], betAmount);
        if (segments[winningSegment].StartsWith("+"))
        {
            balanceChange += betAmount;
        }
        else if (segments[winningSegment].StartsWith("-"))
        {
            balanceChange = betAmount - Math.Abs(balanceChange);
        }

        // Update user balance
        await Service.AddUserBalanceAsync(Context.User.Id, balanceChange, Context.Guild.Id);
        await Service.AddTransactionAsync(Context.User.Id, balanceChange,
            $"Wheel Spin {(segments[winningSegment].Contains('-') ? "Loss" : "Win")}", Context.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(balanceChange > 0 ? "You won!" : "You lost!")
            .WithDescription(
                $"Result: {segments[winningSegment]}. Your balance changed by {balanceChange} {Service.GetCurrencyEmote(Context.Guild.Id)}")
            .WithColor(balanceChange > 0 ? Color.Green : Color.Red)
            .WithImageUrl("attachment://wheelResult.png");

        // Send the image and embed as a message to the channel
        await Context.Channel.SendFileAsync(stream, "wheelResult.png", embed: eb.Build());

        // Helper method to generate weighted random segment
        int GenerateWeightedRandomSegment(int segmentCount, int[] segmentWeights, Random random)
        {
            var totalWeight = segmentWeights.Sum();
            var randomNumber = random.Next(totalWeight);

            var accumulatedWeight = 0;
            for (var i = 0; i < segmentCount; i++)
            {
                accumulatedWeight += segmentWeights[i];
                if (randomNumber < accumulatedWeight)
                    return i;
            }

            return segmentCount - 1; // Return the last segment as a fallback
        }

        // Helper method to compute balance change
        async Task<long> ComputeBalanceChange(string segment, long betAmount)
        {
            long balanceChange = 0;

            if (segment.EndsWith("%"))
            {
                var percent = int.Parse(segment.Substring(1, segment.Length - 2));
                var portion = (long)Math.Ceiling(betAmount * (percent / 100.0));
                balanceChange = segment.StartsWith("-") ? -portion : portion;
            }
            else
            {
                var val = int.Parse(segment.Replace("$", "").Replace("+", "").Replace("-", ""));
                balanceChange = segment.StartsWith("-") ? -val : val;
            }

            return balanceChange;
        }
    }


    [Cmd, Aliases]
    public async Task Transactions(IUser user = null)
    {
        user ??= ctx.User;

        var transactions = await Service.GetTransactionsAsync(user.Id, ctx.Guild.Id);
        transactions = transactions.OrderByDescending(x => x.DateAdded);
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((transactions.Count() - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle("Transactions")
                .WithDescription($"Transactions for {user.Username}")
                .WithColor(Color.Blue);

            for (var i = index * 10; i < (index + 1) * 10 && i < transactions.Count(); i++)
            {
                pageBuilder.AddField($"{i + 1}. {transactions.ElementAt(i).Description}",
                    $"`Amount:` {transactions.ElementAt(i).Amount} {await Service.GetCurrencyEmote(ctx.Guild.Id)}" +
                    $"\n`Date:` {TimestampTag.FromDateTime(transactions.ElementAt(i).DateAdded.Value)}");
            }

            return pageBuilder;
        }
    }

    private static void DrawWheel(SKCanvas canvas, int numSegments, string[] segments, int winningSegment)
    {
        var pastelColor = GeneratePastelColor();
        var colors = new[]
        {
            SKColors.White, pastelColor
        };

        var centerX = canvas.LocalClipBounds.MidX;
        var centerY = canvas.LocalClipBounds.MidY;
        var radius = Math.Min(centerX, centerY) - 10;

        var offsetAngle = 360f / numSegments * winningSegment;

        for (var i = 0; i < numSegments; i++)
        {
            using var paint = new SKPaint();
            paint.Style = SKPaintStyle.Fill;
            paint.Color = colors[i % colors.Length];
            paint.IsAntialias = true;

            var startAngle = (i * 360 / numSegments) - offsetAngle;
            var sweepAngle = 360f / numSegments;

            canvas.DrawArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                startAngle, sweepAngle, true, paint);
        }

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.Black;
        textPaint.TextSize = 20;
        textPaint.IsAntialias = true;
        textPaint.TextAlign = SKTextAlign.Center;

        for (var i = 0; i < numSegments; i++)
        {
            var startAngle = (i * 360 / numSegments) - offsetAngle;
            var middleAngle = startAngle + (360 / numSegments) / 2;
            var textPosition = new SKPoint(
                centerX + (radius * 0.7f) * (float)Math.Cos(DegreesToRadians(middleAngle)),
                centerY + (radius * 0.7f) * (float)Math.Sin(DegreesToRadians(middleAngle)) +
                textPaint.TextSize / 2);

            canvas.DrawText(segments[i], textPosition.X, textPosition.Y, textPaint);
        }

        var arrowShaftLength = radius * 0.2f;
        const float arrowHeadLength = 30;
        var arrowShaftEnd = new SKPoint(centerX, centerY - arrowShaftLength);
        var arrowTip = new SKPoint(centerX, arrowShaftEnd.Y - arrowHeadLength);
        var arrowLeftSide = new SKPoint(centerX - 15, arrowShaftEnd.Y);
        var arrowRightSide = new SKPoint(centerX + 15, arrowShaftEnd.Y);

        using var arrowPaint = new SKPaint();
        arrowPaint.Style = SKPaintStyle.StrokeAndFill;
        arrowPaint.Color = SKColors.Black;
        arrowPaint.IsAntialias = true;

        var arrowPath = new SKPath();
        arrowPath.MoveTo(centerX, centerY);
        arrowPath.LineTo(arrowShaftEnd.X, arrowShaftEnd.Y);

        arrowPath.MoveTo(arrowTip.X, arrowTip.Y);
        arrowPath.LineTo(arrowLeftSide.X, arrowLeftSide.Y);
        arrowPath.LineTo(arrowRightSide.X, arrowRightSide.Y);
        arrowPath.LineTo(arrowTip.X, arrowTip.Y);

        canvas.DrawPath(arrowPath, arrowPaint);
    }


    private static float DegreesToRadians(float degrees)
    {
        return degrees * (float)Math.PI / 180;
    }

    private static SKColor GeneratePastelColor()
    {
        var rand = new Random();
        var hue = (float)rand.Next(0, 361);
        var saturation = 40f + (float)rand.NextDouble() * 20f;
        var lightness = 70f + (float)rand.NextDouble() * 20f;

        return SKColor.FromHsl(hue, saturation, lightness);
    }
}