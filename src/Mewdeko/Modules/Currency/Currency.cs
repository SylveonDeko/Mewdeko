using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency
{
    public class Currency : MewdekoModuleBase<ICurrencyService>
    {
        private readonly InteractiveService _interactive;

        public Currency(InteractiveService interactive)
        {
            _interactive = interactive;
        }

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
                await ReplyAsync($"It was {coinFlip}! You won {betAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}!");
            }
            else
            {
                await Service.AddUserBalanceAsync(Context.User.Id, -betAmount, Context.Guild.Id);
                await Service.AddTransactionAsync(Context.User.Id, -betAmount, "Lost Coin Flip", Context.Guild.Id);
                await ReplyAsync($"It was {coinFlip}. You lost {betAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}.");
            }
        }

        [Cmd, Aliases]
        public async Task DailyReward()
        {
            try
            {
                var recentTransactions = (await Service.GetTransactionsAsync(Context.User.Id, Context.Guild.Id))
                    .Where(t => t.Description == "Daily Reward" && t.DateAdded > DateTime.UtcNow.AddDays(-1));

                if (recentTransactions.Any())
                {
                    await Context.Channel.SendErrorAsync("You already claimed your daily reward. Come back in 24 hours!");
                    return;
                }

                var rewardAmount = 500; // This can be any desired value or a random value.
                await Service.AddUserBalanceAsync(Context.User.Id, rewardAmount, Context.Guild.Id);
                await Service.AddTransactionAsync(Context.User.Id, rewardAmount, "Daily Reward", Context.Guild.Id);
                await Context.Channel.SendConfirmAsync($"You claimed your daily reward of {rewardAmount} {await Service.GetCurrencyEmote(Context.Guild.Id)}!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
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

            await _interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int index)
            {
                var pageBuilder = new PageBuilder()
                    .WithTitle($"Leaderboard")
                    .WithDescription($"Top {users.Count} users in {Context.Guild.Name}")
                    .WithColor(Color.Blue);

                // Add the top 10 users for this page
                for (var i = index * 10; i < (index + 1) * 10 && i < users.Count; i++)
                {
                    var user = await Context.Guild.GetUserAsync(users[i].UserId) ?? (IUser)await Context.Client.GetUserAsync(users[i].UserId);
                    pageBuilder.AddField($"{i + 1}. {user.Username}", $"{users[i].Balance} {await Service.GetCurrencyEmote(Context.Guild.Id)}", inline: true);
                }

                return pageBuilder;
            }
        }
    }
}