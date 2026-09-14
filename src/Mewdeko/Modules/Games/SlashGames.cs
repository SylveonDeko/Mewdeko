using System.Collections.Immutable;
using System.Threading;
using Discord.Interactions;
using LinqToDB;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Hangman.Exceptions;
using Mewdeko.Modules.Games.Common.Kaladont;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

/// <summary>
///     Slash commands for the various chat games.
/// </summary>
[Group("game", "Play games in chat")]
public class SlashGames(IDataConnectionFactory dbFactory, EventHandler handler) : MewdekoSlashModuleBase<GamesService>
{
    /// <summary>
    ///     Term categories available for hangman.
    /// </summary>
    public enum HangmanTermType
    {
        /// <summary>A random category.</summary>
        Random,

        /// <summary>Animal names.</summary>
        Animals,

        /// <summary>Country names.</summary>
        Countries,

        /// <summary>Movie titles.</summary>
        Movies,

        /// <summary>Everyday things.</summary>
        Things
    }

    /// <summary>
    ///     Dictionary languages available for Kaladont.
    /// </summary>
    public enum KaladontLanguage
    {
        /// <summary>English dictionary.</summary>
        En,

        /// <summary>Serbian dictionary.</summary>
        Sr
    }

    private readonly MewdekoRandom rng = new();
    private readonly SemaphoreSlim sem = new(1, 1);

    /// <summary>
    ///     Chooses randomly from a list of options.
    /// </summary>
    /// <param name="list">The list of options separated by semicolons.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("choose", "Chooses randomly from a semicolon separated list of options")]
    [CheckPermissions]
    public async Task Choose([Summary("options", "Options separated by semicolons")] string list)
    {
        var listArr = list.Split(';');
        if (listArr.Length < 2)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.ChoiceMade(ctx.Guild.Id, listArr[rng.Next(0, listArr.Length)]))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Consults the magic 8-ball for an answer.
    /// </summary>
    /// <param name="question">The question to ask.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("eight-ball", "Consults the magic 8-ball for an answer")]
    [CheckPermissions]
    public async Task EightBall([Summary("question", "The question to ask")] string question)
    {
        var res = Service.GetEightballResponse(question);
        var embed = new EmbedBuilder().WithColor(Mewdeko.OkColor)
            .WithDescription(ctx.User.ToString())
            .AddField(efb => efb.WithName(Strings.Question(ctx.Guild.Id)).WithValue(question).WithIsInline(false))
            .AddField(Strings.Eightball(ctx.Guild.Id), res);

        await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Funni interjecting linux command.
    /// </summary>
    /// <param name="guhnoo">The name to replace "GNU".</param>
    /// <param name="loonix">The name to replace "Linux".</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("linux", "I'd just like to interject for a moment")]
    [CheckPermissions]
    public async Task Linux(
        [Summary("guhnoo", "The name to replace GNU")]
        string guhnoo,
        [Summary("loonix", "The name to replace Linux")]
        string loonix)
    {
        await ConfirmAsync(Strings.LinuxCopypasta(ctx.Guild.Id, guhnoo, loonix)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the user's dragon status. Usually used for beta commands.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("dragon", "Toggles your dragon status for beta commands")]
    [CheckPermissions]
    public async Task Dragon()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var user = await dbContext.GetOrCreateUser(ctx.User);
        user.IsDragon = !user.IsDragon;
        await dbContext.UpdateAsync(user);
        await ReplyConfirmAsync(user.IsDragon ? Strings.DragonSet(ctx.Guild.Id) : Strings.DragonUnset(ctx.Guild.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Starts or joins a game of Nunchi.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("nunchi", "Starts or joins a game of Nunchi")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Nunchi()
    {
        var newNunchi = new NunchiGame(ctx.User.Id, ctx.User.ToString());
        NunchiGame nunchi;

        if ((nunchi = Service.NunchiGames.GetOrAdd(ctx.Guild.Id, newNunchi)) != newNunchi)
        {
            if (!await nunchi.Join(ctx.User.Id, ctx.User.ToString()).ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.NunchiAlreadyStarted(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.NunchiJoined(ctx.Guild.Id, nunchi.ParticipantCount))
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await ConfirmAsync(Strings.NunchiCreated(ctx.Guild.Id)).ConfigureAwait(false);
        }
        catch
        {
        }

        nunchi.OnGameEnded += NunchiOnGameEnded;
        nunchi.OnRoundEnded += Nunchi_OnRoundEnded;
        nunchi.OnUserGuessed += Nunchi_OnUserGuessed;
        nunchi.OnRoundStarted += Nunchi_OnRoundStarted;
        handler.Subscribe("MessageReceived", "SlashGames.Nunchi", ClientMessageReceived);

        var success = await nunchi.Initialize().ConfigureAwait(false);
        if (!success)
        {
            if (Service.NunchiGames.TryRemove(ctx.Guild.Id, out var game))
                game.Dispose();
            await ctx.Channel.SendConfirmAsync(Strings.NunchiFailedToStart(ctx.Guild.Id)).ConfigureAwait(false);
        }

        async Task ClientMessageReceived(SocketMessage arg)
        {
            if (arg.Channel.Id != ctx.Channel.Id)
                return;

            if (!int.TryParse(arg.Content, out var number))
                return;
            try
            {
                await nunchi.Input(arg.Author.Id, arg.Author.ToString(), number).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        async Task NunchiOnGameEnded(NunchiGame arg1, string? arg2)
        {
            if (Service.NunchiGames.TryRemove(ctx.Guild.Id, out var game))
            {
                handler.Unsubscribe("MessageReceived", "SlashGames.Nunchi", ClientMessageReceived);
                game.Dispose();
            }

            if (arg2 == null)
                await ctx.Channel.SendConfirmAsync(Strings.NunchiEndedNoWinner(ctx.Guild.Id));
            else
                await ctx.Channel.SendConfirmAsync(Strings.NunchiEnded(ctx.Guild.Id, Format.Bold(arg2)));
        }
    }

    private Task Nunchi_OnRoundStarted(NunchiGame arg, int cur)
    {
        return ctx.Channel.SendConfirmAsync(Strings.NunchiRoundStarted(ctx.Guild.Id,
            Format.Bold(arg.ParticipantCount.ToString()),
            Format.Bold(cur.ToString())));
    }

    private Task Nunchi_OnUserGuessed(NunchiGame arg)
    {
        return ctx.Channel.SendConfirmAsync(Strings.NunchiNextNumber(ctx.Guild.Id,
            Format.Bold(arg.CurrentNumber.ToString())));
    }

    private Task Nunchi_OnRoundEnded(NunchiGame arg1, (ulong Id, string Name)? arg2)
    {
        if (arg2.HasValue)
            return ctx.Channel.SendConfirmAsync(Strings.NunchiRoundEnded(ctx.Guild.Id, Format.Bold(arg2.Value.Name)));
        return ctx.Channel.SendConfirmAsync(Strings.NunchiRoundEndedBoot(ctx.Guild.Id,
            Format.Bold($"\n{string.Join("\n, ", arg1.Participants.Select(x => x.Name))}")));
    }

    /// <summary>
    ///     Starts a game of TicTacToe, or joins the one waiting in this channel.
    /// </summary>
    /// <param name="turnTimer">Turn time in seconds. Default 15.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("tic-tac-toe", "Starts or joins a game of TicTacToe")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task TicTacToe(
        [Summary("turn-timer", "Turn time in seconds (5-60). Default 15")]
        int? turnTimer = null)
    {
        var options = new TicTacToe.Options();
        if (turnTimer.HasValue)
            options.TurnTimer = turnTimer.Value;
        options.NormalizeOptions();

        var channel = (ITextChannel)ctx.Channel;

        await sem.WaitAsync(1000).ConfigureAwait(false);
        try
        {
            if (Service.TicTacToeGames.TryGetValue(channel.Id, out var game))
            {
                await EphemeralReplyConfirmAsync(Strings.TttCreated(ctx.Guild.Id)).ConfigureAwait(false);
                _ = game.Start((IGuildUser)ctx.User);
                return;
            }

            game = new TicTacToe(Strings, channel, (IGuildUser)ctx.User, options, Config, handler);
            Service.TicTacToeGames.Add(channel.Id, game);
            await ReplyConfirmAsync(Strings.TttCreated(ctx.Guild.Id)).ConfigureAwait(false);

            game.OnEnded += _ =>
            {
                Service.TicTacToeGames.Remove(channel.Id);
                sem.Dispose();
            };
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    ///     Starts an Acrophobia game.
    /// </summary>
    /// <param name="submissionTime">Time in seconds after which submissions close and voting starts.</param>
    /// <param name="voteTime">Time in seconds after which voting closes and the winner is declared.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("acrophobia", "Starts an Acrophobia game")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Acrophobia(
        [Summary("submission-time", "Seconds until submissions close (15-300). Default 60")]
        int? submissionTime = null,
        [Summary("vote-time", "Seconds until voting closes (15-120). Default 30")]
        int? voteTime = null)
    {
        var options = new AcrophobiaGame.Options();
        if (submissionTime.HasValue)
            options.SubmissionTime = submissionTime.Value;
        if (voteTime.HasValue)
            options.VoteTime = voteTime.Value;
        options.NormalizeOptions();

        var channel = (ITextChannel)ctx.Channel;

        var game = new AcrophobiaGame(options);
        if (Service.AcrophobiaGames.TryAdd(channel.Id, game))
        {
            try
            {
                game.OnStarted += Game_OnStarted;
                game.OnEnded += Game_OnEnded;
                game.OnVotingStarted += Game_OnVotingStarted;
                game.OnUserVoted += Game_OnUserVoted;
                handler.Subscribe("MessageReceived", "SlashGames.Acrophobia", ClientMessageReceived);
                await game.Run().ConfigureAwait(false);
            }
            finally
            {
                handler.Unsubscribe("MessageReceived", "SlashGames.Acrophobia", ClientMessageReceived);
                Service.AcrophobiaGames.TryRemove(channel.Id, out game);
                game.Dispose();
            }
        }
        else
        {
            await ReplyErrorAsync(Strings.AcroRunning(ctx.Guild.Id)).ConfigureAwait(false);
        }

        async Task ClientMessageReceived(SocketMessage msg)
        {
            if (msg.Channel.Id != ctx.Channel.Id)
                return;

            try
            {
                var success = await game.UserInput(msg.Author.Id, msg.Author.ToString(), msg.Content)
                    .ConfigureAwait(false);
                if (success)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private Task Game_OnStarted(AcrophobiaGame game)
    {
        var embed = new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.Acrophobia(ctx.Guild.Id))
            .WithDescription(Strings.AcroStarted(ctx.Guild.Id, Format.Bold(string.Join(".", game.StartingLetters))))
            .WithFooter(efb => efb.WithText(Strings.AcroStartedFooter(ctx.Guild.Id, game.Opts.SubmissionTime)));

        return SendEmbedAsync(embed);
    }

    private Task Game_OnUserVoted(string user)
    {
        return ctx.Channel.SendConfirmAsync(
            Strings.Acrophobia(ctx.Guild.Id),
            Strings.AcroVoteCast(ctx.Guild.Id, Format.Bold(user)));
    }

    private async Task Game_OnVotingStarted(AcrophobiaGame game,
        ImmutableArray<KeyValuePair<AcrophobiaUser, int>> submissions)
    {
        switch (submissions.Length)
        {
            case 0:
                await ctx.Channel.SendErrorAsync(Strings.Acrophobia(ctx.Guild.Id),
                        Strings.AcroEndedNoSub(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            case 1:
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithDescription(
                            Strings.AcroWinnerOnly(ctx.Guild.Id,
                                Format.Bold(submissions.First().Key.UserName)))
                        .WithFooter(efb => efb.WithText(submissions.First().Key.Input)))
                    .ConfigureAwait(false);
                return;
        }

        var i = 0;
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"{Strings.Acrophobia(ctx.Guild.Id)} - {Strings.SubmissionsClosed(ctx.Guild.Id)}")
            .WithDescription(Strings.AcroNymWas(ctx.Guild.Id,
                $"{Format.Bold(string.Join(".", game.StartingLetters))}\n--\n{submissions.Aggregate("", (agg, cur) => $"{agg}`{++i}.` **{cur.Key.Input}**\n")}\n--"))
            .WithFooter(efb => efb.WithText(Strings.AcroVote(ctx.Guild.Id)));

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    private async Task Game_OnEnded(AcrophobiaGame game,
        ImmutableArray<KeyValuePair<AcrophobiaUser, int>> votes)
    {
        if (!votes.Any() || votes.All(x => x.Value == 0))
        {
            await ctx.Channel
                .SendErrorAsync(Strings.Acrophobia(ctx.Guild.Id), Strings.AcroNoVotesCast(ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        var table = votes.OrderByDescending(v => v.Value);
        var winner = table.First();
        var embed = new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.Acrophobia(ctx.Guild.Id))
            .WithDescription(Strings.AcroWinner(ctx.Guild.Id, Format.Bold(winner.Key.UserName),
                Format.Bold(winner.Value.ToString())))
            .WithFooter(efb => efb.WithText(winner.Key.Input));

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    private async Task SendEmbedAsync(EmbedBuilder embed)
    {
        if (ctx.Interaction.HasResponded)
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        else
            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Speed typing game commands.
    /// </summary>
    [Group("typing", "Speed typing contests")]
    public class GameTyping(DiscordShardedClient client, GuildSettingsService guildSettings, EventHandler handler)
        : MewdekoSlashSubmodule<GamesService>
    {
        /// <summary>
        ///     Starts a speed typing game.
        /// </summary>
        /// <param name="startTime">How long in seconds it takes for the race to start. Default 5.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("start", "Starts a speed typing contest")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TypeStart(
            [Summary("start-time", "Seconds until the race starts (3-30). Default 5")]
            int? startTime = null)
        {
            var options = new TypingGame.Options();
            if (startTime.HasValue)
                options.StartTime = startTime.Value;
            options.NormalizeOptions();

            var channel = (ITextChannel)ctx.Channel;
            var prefix = await guildSettings.GetPrefix(ctx.Guild);

            var game = Service.RunningContests.GetOrAdd(channel.Guild.Id,
                _ => new TypingGame(Service, client, channel, prefix, options, handler, Strings));

            if (game.IsActive)
            {
                await ReplyErrorAsync(Strings.TypingContestRunning(ctx.Guild.Id, game.Channel.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                await EphemeralReplyConfirmAsync(Strings.GameStarting(ctx.Guild.Id, "typing contest"))
                    .ConfigureAwait(false);
                await game.Start().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Stops the current speed typing game.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("stop", "Stops the current speed typing contest")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TypeStop()
        {
            var channel = (ITextChannel)ctx.Channel;
            if (Service.RunningContests.TryRemove(channel.Guild.Id, out var game))
            {
                await EphemeralReplyConfirmAsync(Strings.TypingContestStopped(ctx.Guild.Id)).ConfigureAwait(false);
                await game.Stop().ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.TypingNoContest(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the articles available for the typing game.
        /// </summary>
        /// <param name="page">The page number to display.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists the articles available for the typing game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TypeList([Summary("page", "The page number to display")] int page = 1)
        {
            if (page < 1)
                page = 1;

            var articles = Service.TypingArticles.Skip((page - 1) * 15).Take(15).ToArray();

            if (articles.Length == 0)
            {
                await ErrorAsync(Strings.NoArticlesFound(ctx.Guild.Id, ctx.User.Mention)).ConfigureAwait(false);
                return;
            }

            var i = (page - 1) * 15;
            await ConfirmAsync(Strings.TypingArticleList(ctx.Guild.Id,
                string.Join("\n", articles.Select(a => $"`#{++i}` - {a.Text.TrimTo(50)}")))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a typing article by its index.
        /// </summary>
        /// <param name="index">The index of the article to delete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("delete", "Deletes a typing article by its index")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashOwnerOnly]
        public async Task TypeDelete([Summary("index", "The index of the article to delete")] int index)
        {
            var removed = Service.RemoveTypingArticle(--index);

            if (removed is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.TypingArticleRemoved(ctx.Guild.Id, index + 1))
                .WithDescription(removed.Text.TrimTo(50))
                .WithOkColor();

            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Opens a modal to add a new article for the typing game.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Adds a new article for the typing game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashOwnerOnly]
        public Task TypeAdd()
        {
            return RespondWithModalAsync<TypingArticleModal>("games_typing_add");
        }

        /// <summary>
        ///     Handles the typing article modal submission and adds the article.
        /// </summary>
        /// <param name="modal">The submitted modal.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [ModalInteraction("games_typing_add", true)]
        [SlashOwnerOnly]
        public async Task TypeAddSubmitted(TypingArticleModal modal)
        {
            if (string.IsNullOrWhiteSpace(modal.Text))
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            Service.AddTypingArticle(ctx.User, modal.Text);

            await ConfirmAsync(Strings.TypingArticleAdded(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Hangman game commands.
    /// </summary>
    [Group("hangman", "Hangman games")]
    public class GameHangman(EventHandler handler) : MewdekoSlashSubmodule<GamesService>
    {
        /// <summary>
        ///     Starts a hangman game with the specified term type.
        /// </summary>
        /// <param name="type">The type of hangman game to start.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("start", "Starts a hangman game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HangmanStart(
            [Summary("type", "The category of terms to use")]
            HangmanTermType type = HangmanTermType.Random)
        {
            Hangman hm;
            try
            {
                hm = new Hangman(type.ToString().ToLowerInvariant(), Service.TermPool);
            }
            catch (TermNotFoundException)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!Service.HangmanGames.TryAdd(ctx.Channel.Id, hm))
            {
                hm.Dispose();
                await ReplyErrorAsync(Strings.HangmanRunning(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            hm.OnGameEnded += Hm_OnGameEnded;
            hm.OnGuessFailed += Hm_OnGuessFailed;
            hm.OnGuessSucceeded += Hm_OnGuessSucceeded;
            hm.OnLetterAlreadyUsed += Hm_OnLetterAlreadyUsed;
            handler.Subscribe("MessageReceived", "SlashGames.Hangman", ClientMessageReceived);

            try
            {
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle($"{Strings.HangmanGameStarted(ctx.Guild.Id)} ({hm.TermType})")
                    .WithDescription($"{hm.ScrambledWord}\n{hm.GetHangman()}");
                await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            catch
            {
            }

            await hm.EndedTask.ConfigureAwait(false);

            handler.Unsubscribe("MessageReceived", "SlashGames.Hangman", ClientMessageReceived);
            Service.HangmanGames.TryRemove(ctx.Channel.Id, out _);
            hm.Dispose();
            return;

            async Task ClientMessageReceived(SocketMessage msg)
            {
                if (ctx.Channel.Id == msg.Channel.Id && !msg.Author.IsBot)
                    await hm.Input(msg.Author.Id, msg.Author.ToString(), msg.Content);
            }
        }

        /// <summary>
        ///     Stops the currently running hangman game in the current channel.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("stop", "Stops the hangman game in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HangmanStop()
        {
            if (Service.HangmanGames.TryRemove(ctx.Channel.Id, out var removed))
            {
                await removed.Stop().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.HangmanStopped(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.HangmanNotRunning(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the available hangman term types.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists the available hangman term types")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HangmanList()
        {
            await ConfirmAsync(
                    $"{Format.Code(Strings.HangmanTypes(ctx.Guild.Id, "/game "))}\n{string.Join("\n", Service.TermPool.Data.Keys)}")
                .ConfigureAwait(false);
        }

        private Task Hm_OnGameEnded(Hangman game, string? winner)
        {
            if (winner == null)
            {
                var loseEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                    .WithDescription(Format.Bold(Strings.HangmanLose(ctx.Guild.Id)))
                    .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                    .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                    .WithErrorColor();

                if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                    loseEmbed.WithImageUrl(game.Term.ImageUrl);

                return ctx.Channel.EmbedAsync(loseEmbed);
            }

            var winEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                .WithDescription(Format.Bold(Strings.HangmanWin(ctx.Guild.Id, winner)))
                .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                .WithOkColor();

            if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                winEmbed.WithImageUrl(game.Term.ImageUrl);

            return ctx.Channel.EmbedAsync(winEmbed);
        }

        private Task Hm_OnLetterAlreadyUsed(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` has already been used. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }

        private Task Hm_OnGuessSucceeded(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendConfirmAsync($"Hangman Game ({game.TermType})",
                $"{user} guessed a letter `{guess}`!\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }

        private Task Hm_OnGuessFailed(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` does not exist. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }
    }

    /// <summary>
    ///     Kaladont word chain game commands.
    /// </summary>
    [Group("kaladont", "Kaladont word chain games")]
    public class GameKaladont(EventHandler handler, KaladontChannelService channelService)
        : MewdekoSlashSubmodule<GamesService>
    {
        /// <summary>
        ///     Starts a Kaladont game.
        /// </summary>
        /// <param name="language">Language for the game dictionary (en or sr).</param>
        /// <param name="joinTime">Time in seconds for players to join the game.</param>
        /// <param name="turnTime">Time in seconds for each player's turn.</param>
        /// <param name="minPlayers">Minimum number of players required to start the game.</param>
        /// <param name="endless">Enable endless mode, which prevents words that would make the next turn impossible.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("start", "Starts a Kaladont game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task KaladontStart(
            [Summary("language", "Dictionary language. Default en")]
            KaladontLanguage language = KaladontLanguage.En,
            [Summary("join-time", "Seconds for players to join (10-60). Default 15")]
            int? joinTime = null,
            [Summary("turn-time", "Seconds per turn (15-120). Default 30")]
            int? turnTime = null,
            [Summary("min-players", "Minimum players to start (2-10). Default 2")]
            int? minPlayers = null,
            [Summary("endless", "Enable endless mode")]
            bool endless = false)
        {
            var options = BuildOptions(language, joinTime, turnTime, minPlayers, endless);
            var channel = (ITextChannel)ctx.Channel;

            var dictionary = Service.GetKaladontDictionary(options.Language);
            if (dictionary.Count == 0)
            {
                await ReplyErrorAsync(Strings.KaladontDictNotLoaded(ctx.Guild.Id, options.Language))
                    .ConfigureAwait(false);
                return;
            }

            var startingWord = Service.GetRandomKaladontStartingWord(options.Language);

            var game = new KaladontGame(options, startingWord, dictionary);
            var joinEmote = Config.SuccessEmote.ToIEmote();

            if (Service.KaladontGames.TryAdd(channel.Id, game))
            {
                try
                {
                    game.OnGameStarted += Game_OnStarted;
                    game.OnPlayerTurn += Game_OnPlayerTurn;
                    game.OnWordPlayed += Game_OnWordPlayed;
                    game.OnPlayerEliminated += Game_OnPlayerEliminated;
                    game.OnGameEnded += Game_OnGameEnded;

                    handler.Subscribe("MessageReceived", "SlashGames.Kaladont", ClientMessageReceived);
                    handler.Subscribe("ReactionAdded", "SlashGames.Kaladont", ClientReactionAdded);

                    await ShowJoinPhase(game, joinEmote).ConfigureAwait(false);

                    var success = await game.Initialize().ConfigureAwait(false);

                    if (!success)
                    {
                        await ctx.Channel.SendErrorAsync(
                            Strings.Kaladont(ctx.Guild.Id),
                            Strings.KaladontNotEnoughPlayers(ctx.Guild.Id, game.Opts.MinPlayers)
                        ).ConfigureAwait(false);
                        return;
                    }

                    await game.EndedTask.ConfigureAwait(false);
                }
                finally
                {
                    handler.Unsubscribe("MessageReceived", "SlashGames.Kaladont", ClientMessageReceived);
                    handler.Unsubscribe("ReactionAdded", "SlashGames.Kaladont", ClientReactionAdded);
                    Service.KaladontGames.TryRemove(channel.Id, out game);
                    game?.Dispose();
                }
            }
            else
            {
                await ReplyErrorAsync(Strings.KaladontAlreadyRunning(ctx.Guild.Id)).ConfigureAwait(false);
            }

            async Task ClientMessageReceived(SocketMessage msg)
            {
                if (msg.Channel.Id != ctx.Channel.Id || msg.Author.IsBot)
                    return;

                if (!Service.KaladontGames.TryGetValue(channel.Id, out var activeGame) ||
                    activeGame.CurrentPhase == KaladontGame.Phase.Ended)
                    return;

                var content = msg.Content?.Trim();
                if (string.IsNullOrEmpty(content))
                    return;

                try
                {
                    if (content.Equals("kaladont", StringComparison.OrdinalIgnoreCase))
                    {
                        var success = await activeGame.SayKaladont(msg.Author.Id).ConfigureAwait(false);
                        if (success)
                        {
                            try
                            {
                                await msg.DeleteAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                            }
                        }

                        return;
                    }

                    var (wordSuccess, validationResult) =
                        await activeGame.PlayWord(msg.Author.Id, content).ConfigureAwait(false);

                    if (wordSuccess || validationResult != KaladontGame.ValidationResult.Valid)
                    {
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        if (!wordSuccess && validationResult != KaladontGame.ValidationResult.Valid)
                        {
                            var errorMessage = GetValidationErrorMessage(validationResult, content);
                            await channel.SendErrorAsync(Strings.Kaladont(ctx.Guild.Id), errorMessage)
                                .ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }

            async Task ClientReactionAdded(Cacheable<IUserMessage, ulong> message,
                Cacheable<IMessageChannel, ulong> reactionChannel, SocketReaction reaction)
            {
                if (reactionChannel.Id != ctx.Channel.Id || reaction.UserId == ctx.Client.CurrentUser.Id)
                    return;

                if (!Service.KaladontGames.TryGetValue(channel.Id, out var activeGame) ||
                    activeGame.CurrentPhase == KaladontGame.Phase.Ended)
                    return;

                try
                {
                    if (activeGame.CurrentPhase == KaladontGame.Phase.Joining &&
                        reaction.Emote.Name == joinEmote?.Name)
                    {
                        var user = await ctx.Guild.GetUserAsync(reaction.UserId).ConfigureAwait(false);
                        if (user == null || user.IsBot)
                            return;

                        var joined = await activeGame.Join(user.Id, user.ToString()).ConfigureAwait(false);
                        if (joined)
                        {
                            await channel.SendConfirmAsync(
                                Strings.KaladontPlayerJoined(ctx.Guild.Id, Format.Bold(user.ToString()),
                                    activeGame.Players.Length, activeGame.Opts.MinPlayers)
                            ).ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        /// <summary>
        ///     Sets up a persistent Kaladont channel.
        /// </summary>
        /// <param name="language">Language for the game dictionary (en or sr).</param>
        /// <param name="endless">Enable endless mode.</param>
        /// <param name="turnTime">Time in seconds for each player's turn.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("setup", "Sets up this channel as a persistent Kaladont channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task KaladontSetup(
            [Summary("language", "Dictionary language. Default en")]
            KaladontLanguage language = KaladontLanguage.En,
            [Summary("endless", "Enable endless mode")]
            bool endless = false,
            [Summary("turn-time", "Seconds per turn (15-120). Default 30")]
            int? turnTime = null)
        {
            var options = BuildOptions(language, null, turnTime, null, endless);
            var channel = (ITextChannel)ctx.Channel;

            var dictionary = Service.GetKaladontDictionary(options.Language);
            if (dictionary.Count == 0)
            {
                await ReplyErrorAsync(Strings.KaladontDictNotLoaded(ctx.Guild.Id, options.Language))
                    .ConfigureAwait(false);
                return;
            }

            var mode = options.Endless ? 1 : 0;
            var (success, startingWord) = await channelService.SetupChannel(
                ctx.Guild.Id,
                channel.Id,
                options.Language,
                mode,
                options.TurnTime
            );

            if (success)
            {
                var modeText = options.Endless ? "Endless" : "Normal";
                var lastTwo = startingWord.Length >= 2 ? startingWord[^2..].ToUpperInvariant() : "";

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.KaladontChannelActive(ctx.Guild.Id))
                    .WithDescription($"**Language:** {options.Language.ToUpperInvariant()} | **Mode:** {modeText}")
                    .AddField(Strings.KaladontCurrentWordLabel(ctx.Guild.Id), $"# {startingWord.ToUpperInvariant()}")
                    .AddField(Strings.KaladontNextMustStart(ctx.Guild.Id), Format.Bold(lastTwo), true)
                    .AddField(Strings.KaladontHowToPlay(ctx.Guild.Id), Strings.KaladontTypeToJoin(ctx.Guild.Id), true);

                await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.KaladontDictNotLoaded(ctx.Guild.Id, options.Language))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Stops the current Kaladont game.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("stop", "Stops the Kaladont game in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task KaladontStop()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.KaladontGames.TryGetValue(channel.Id, out var game))
            {
                await game.StopGame().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.KaladontStopped(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.KaladontNotRunning(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Disables the persistent Kaladont channel.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("disable", "Disables the persistent Kaladont channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task KaladontDisable()
        {
            var success = await channelService.DisableChannel(ctx.Channel.Id);

            if (success)
            {
                await ReplyConfirmAsync(Strings.KaladontDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.KaladontNotSetup(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        private static KaladontGame.Options BuildOptions(KaladontLanguage language, int? joinTime, int? turnTime,
            int? minPlayers, bool endless)
        {
            var options = new KaladontGame.Options
            {
                Language = language.ToString().ToLowerInvariant(), Endless = endless
            };
            if (joinTime.HasValue)
                options.JoinTime = joinTime.Value;
            if (turnTime.HasValue)
                options.TurnTime = turnTime.Value;
            if (minPlayers.HasValue)
                options.MinPlayers = minPlayers.Value;
            options.NormalizeOptions();
            return options;
        }

        private async Task ShowJoinPhase(KaladontGame game, IEmote? joinEmote)
        {
            var modeText = game.Opts.Mode == KaladontGame.GameMode.Endless ? "Endless" : "Normal";

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.Kaladont(ctx.Guild.Id))
                .WithDescription(Strings.KaladontJoinPhase(ctx.Guild.Id, game.Opts.JoinTime, game.Opts.MinPlayers))
                .AddField(Strings.KaladontRules(ctx.Guild.Id, game.Opts.TurnTime), "​")
                .WithFooter(
                    $"Language: {game.Opts.Language.ToUpperInvariant()} | Mode: {modeText} | Join time: {game.Opts.JoinTime}s");

            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            var msg = await ctx.Interaction.GetOriginalResponseAsync().ConfigureAwait(false);
            if (joinEmote != null)
                await msg.AddReactionAsync(joinEmote).ConfigureAwait(false);
        }

        private Task Game_OnStarted(KaladontGame game)
        {
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.Kaladont(ctx.Guild.Id))
                .WithDescription(Strings.KaladontGameStarted(ctx.Guild.Id,
                    Format.Bold(game.CurrentWord),
                    game.CurrentPlayer != null ? Format.Bold(game.CurrentPlayer.UserName) : "?"))
                .AddField("Players", string.Join("\n", game.Players.Select((p, i) => $"{i + 1}. {p.UserName}")), true)
                .AddField("Starting Word", Format.Bold(game.CurrentWord.ToUpperInvariant()), true)
                .WithFooter(Strings.KaladontGameStartedFooter(ctx.Guild.Id, game.Opts.TurnTime));

            return ctx.Channel.EmbedAsync(embed);
        }

        private Task Game_OnPlayerTurn(KaladontGame game, KaladontPlayer player)
        {
            var lastTwo = game.CurrentWord[^2..].ToUpperInvariant();

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle(Strings.Kaladont(ctx.Guild.Id))
                .WithDescription(Strings.KaladontPlayerTurn(ctx.Guild.Id,
                    Format.Bold(player.UserName),
                    Format.Bold(game.CurrentWord),
                    Format.Bold(lastTwo)))
                .AddField("Last 2 Letters", Format.Bold(lastTwo), true)
                .AddField("Words Used", game.UsedWordsCount.ToString(), true);

            if (game.RecentWords.Length > 0)
            {
                embed.AddField("Recent Words",
                    string.Join(" -> ", game.RecentWords.TakeLast(5).Select(w => Format.Code(w))));
            }

            embed.WithFooter(Strings.KaladontTurnRemaining(ctx.Guild.Id, game.Opts.TurnTime));

            return ctx.Channel.EmbedAsync(embed);
        }

        private Task Game_OnWordPlayed(KaladontGame game, KaladontPlayer player, string word)
        {
            var isSerbianLanguage = game.Opts.Language.ToLowerInvariant() == "sr";

            var nextPrefix = isSerbianLanguage
                ? SerbianDigraphHelper.GetLastTwoLetters(word)
                : word.Length >= 2
                    ? word[^2..]
                    : word;

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"{Config.SuccessEmote} {Strings.KaladontWordAccepted(ctx.Guild.Id)}")
                .WithDescription(Strings.KaladontWordPlayed(ctx.Guild.Id, Format.Bold(player.UserName),
                    Format.Bold(word.ToUpperInvariant())))
                .AddField(Strings.KaladontLastWord(ctx.Guild.Id), Format.Bold(word.ToUpperInvariant()), true)
                .AddField(Strings.KaladontNextPrefix(ctx.Guild.Id), Format.Bold(nextPrefix.ToUpperInvariant()), true);

            return ctx.Channel.EmbedAsync(embed);
        }

        private async Task Game_OnPlayerEliminated(KaladontGame game, KaladontPlayer player, string reason)
        {
            var reasonText = reason switch
            {
                "timeout" => Strings.KaladontPlayerTimeout(ctx.Guild.Id),
                "kaladont" => Strings.KaladontUserSaidKaladont(ctx.Guild.Id),
                "too_short" => Strings.KaladontInvalidLength(ctx.Guild.Id),
                "already_used" => Strings.KaladontAlreadyUsed(ctx.Guild.Id, ""),
                "wrong_letters" => Strings.KaladontWrongLetters(ctx.Guild.Id, ""),
                "kaladont_loop" => Strings.KaladontLoopDetected(ctx.Guild.Id, ""),
                "not_in_dictionary" => Strings.KaladontNotFound(ctx.Guild.Id, ""),
                "dead_end" => Strings.KaladontDeadEnd(ctx.Guild.Id, "", ""),
                _ => reason
            };

            var embed = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.Kaladont(ctx.Guild.Id))
                .WithDescription(Strings.KaladontEliminated(ctx.Guild.Id, Format.Bold(player.UserName), reasonText))
                .AddField("Players Remaining", game.Players.Length.ToString());

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private Task Game_OnGameEnded(KaladontGame game, KaladontPlayer? winner)
        {
            var modeText = game.Opts.Mode == KaladontGame.GameMode.Endless ? "Endless" : "Normal";

            var embed = new EmbedBuilder()
                .WithTitle(Strings.KaladontGameEnded(ctx.Guild.Id))
                .AddField("Total Words", game.UsedWordsCount.ToString(), true)
                .AddField("Language", game.Opts.Language.ToUpperInvariant(), true)
                .AddField("Mode", modeText, true);

            if (winner != null)
            {
                embed.WithOkColor()
                    .WithDescription(Strings.KaladontWinner(ctx.Guild.Id, Format.Bold(winner.UserName)));
            }
            else
            {
                embed.WithErrorColor()
                    .WithDescription(Strings.KaladontNoWinner(ctx.Guild.Id));
            }

            return ctx.Channel.EmbedAsync(embed);
        }

        private string GetValidationErrorMessage(KaladontGame.ValidationResult result, string word)
        {
            return result switch
            {
                KaladontGame.ValidationResult.TooShort => Strings.KaladontInvalidLength(ctx.Guild.Id),
                KaladontGame.ValidationResult.AlreadyUsed => Strings.KaladontAlreadyUsed(ctx.Guild.Id,
                    Format.Bold(word)),
                KaladontGame.ValidationResult.WrongLetters => Strings.KaladontWrongLetters(ctx.Guild.Id, ""),
                KaladontGame.ValidationResult.KaladontLoop => Strings.KaladontLoopDetected(ctx.Guild.Id,
                    Format.Bold(word)),
                KaladontGame.ValidationResult.NotInDictionary => Strings.KaladontNotFound(ctx.Guild.Id,
                    Format.Bold(word)),
                KaladontGame.ValidationResult.DeadEnd => Strings.KaladontDeadEnd(ctx.Guild.Id,
                    Format.Bold(word),
                    Format.Bold(word.Length >= 2 ? word[^2..].ToUpperInvariant() : "")),
                _ => "Invalid word"
            };
        }
    }

    /// <summary>
    ///     Trivia game commands.
    /// </summary>
    [Group("trivia", "Trivia games")]
    public class GameTrivia(IDataCache cache, GamesConfigService gamesConfig, EventHandler handler)
        : MewdekoSlashSubmodule<GamesService>
    {
        /// <summary>
        ///     Starts a trivia game.
        /// </summary>
        /// <param name="pokemon">Whether it's "Who's that pokemon?" trivia.</param>
        /// <param name="noHint">Don't show any hints.</param>
        /// <param name="winRequirement">Winning requirement. Set 0 for an infinite game. Default 10.</param>
        /// <param name="questionTimer">How long until the question ends. Default 30.</param>
        /// <param name="timeout">Number of questions of inactivity in order to stop. Set 0 for never. Default 10.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("start", "Starts a trivia game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TriviaStart(
            [Summary("pokemon", "Play Who's that pokemon trivia")]
            bool pokemon = false,
            [Summary("no-hint", "Don't show any hints")]
            bool noHint = false,
            [Summary("win-req", "Points needed to win, 0 for infinite. Default 10")]
            int? winRequirement = null,
            [Summary("question-timer", "Seconds per question (10-300). Default 30")]
            int? questionTimer = null,
            [Summary("timeout", "Inactive questions before stopping (0-20). Default 10")]
            int? timeout = null)
        {
            var channel = (ITextChannel)ctx.Channel;

            var opts = new TriviaOptions
            {
                IsPokemon = pokemon, NoHint = noHint
            };
            if (winRequirement.HasValue)
                opts.WinRequirement = winRequirement.Value;
            if (questionTimer.HasValue)
                opts.QuestionTimer = questionTimer.Value;
            if (timeout.HasValue)
                opts.Timeout = timeout.Value;
            opts.NormalizeOptions();

            var config = gamesConfig.Data;
            if (config.Trivia.MinimumWinReq > 0 && config.Trivia.MinimumWinReq > opts.WinRequirement)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var trivia = new TriviaGame(Strings, cache, channel.Guild, channel, opts, "/game trivia stop", handler);
            if (Service.RunningTrivias.TryAdd(channel.Guild.Id, trivia))
            {
                await EphemeralReplyConfirmAsync(Strings.GameStarting(ctx.Guild.Id, Strings.TriviaGame(ctx.Guild.Id)))
                    .ConfigureAwait(false);
                try
                {
                    await trivia.StartGame().ConfigureAwait(false);
                }
                finally
                {
                    Service.RunningTrivias.TryRemove(channel.Guild.Id, out trivia);
                    await trivia.EnsureStopped().ConfigureAwait(false);
                }

                return;
            }

            await ErrorAsync($"{Strings.TriviaAlreadyRunning(ctx.Guild.Id)}\n{trivia.CurrentQuestion}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Stops the current trivia game.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("stop", "Stops the current trivia game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Tq()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.RunningTrivias.TryGetValue(channel.Guild.Id, out var trivia))
            {
                await EphemeralReplyConfirmAsync(Strings.TriviaStopping(ctx.Guild.Id)).ConfigureAwait(false);
                await trivia.StopGame().ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.TriviaNone(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the current trivia leaderboard.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("leaderboard", "Shows the current trivia leaderboard")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TriviaLeaderboard()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.RunningTrivias.TryGetValue(channel.Guild.Id, out var trivia))
            {
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.Leaderboard(ctx.Guild.Id))
                    .WithDescription(trivia.GetLeaderboard());
                await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.TriviaNone(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}