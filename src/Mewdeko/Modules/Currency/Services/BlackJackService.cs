using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Common;
using Mewdeko.Services.Strings;
using Npgsql;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     What happened when a player tried to sit at a table.
/// </summary>
public enum BlackjackSitStatus
{
    /// <summary>
    ///     The player opened a new table.
    /// </summary>
    Opened,

    /// <summary>
    ///     The player joined the open table.
    /// </summary>
    Joined,

    /// <summary>
    ///     The table already has five players.
    /// </summary>
    Full,

    /// <summary>
    ///     The player already sits at a table in this guild.
    /// </summary>
    AlreadySeated,

    /// <summary>
    ///     The table in this channel is past its joining window.
    /// </summary>
    InProgress,

    /// <summary>
    ///     The wallet did not hold the bet.
    /// </summary>
    InsufficientFunds
}

/// <summary>
///     What happened to a blackjack move.
/// </summary>
public enum BlackjackActionStatus
{
    /// <summary>
    ///     The move was applied.
    /// </summary>
    Applied,

    /// <summary>
    ///     There is no table.
    /// </summary>
    NoGame,

    /// <summary>
    ///     The player does not sit at the table.
    /// </summary>
    NotSeated,

    /// <summary>
    ///     It is another player's turn.
    /// </summary>
    NotYourTurn,

    /// <summary>
    ///     The press belongs to an older message of the table.
    /// </summary>
    OtherMessage,

    /// <summary>
    ///     The table moved on since the press was made, or another request changed it first.
    /// </summary>
    Stale,

    /// <summary>
    ///     The move is not legal right now.
    /// </summary>
    NotAllowed,

    /// <summary>
    ///     The wallet did not hold the extra stake the move needs.
    /// </summary>
    InsufficientFunds
}

/// <summary>
///     A blackjack table as the commands and buttons see it.
/// </summary>
public sealed class BlackjackSession
{
    /// <summary>
    ///     Gets the guild the table is in.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    ///     Gets the channel the table is in.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    ///     Gets the table message, zero until it is known.
    /// </summary>
    public ulong MessageId { get; init; }

    /// <summary>
    ///     Gets the saved version, which changes on every step.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    ///     Gets the seats and cards.
    /// </summary>
    public BlackjackTable State { get; init; } = new();

    /// <summary>
    ///     Gets when the table was opened.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    ///     Gets when the table last changed phase or turn.
    /// </summary>
    public DateTime LastActionAt { get; init; }

    /// <summary>
    ///     Gets how every seat was paid, or null while the table is still open or being played.
    /// </summary>
    public IReadOnlyList<BlackjackSeatResult>? Results { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the last step happened because a player ran out of time.
    /// </summary>
    public bool Expired { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the last step handed the turn to a new player.
    /// </summary>
    public bool TurnChanged { get; init; }

    /// <summary>
    ///     Gets the moment the current phase runs out, or null once the table is paid out.
    /// </summary>
    public DateTime? Deadline
    {
        get
        {
            return Results is not null ? null : DeadlineOf(State.Phase, StartedAt, LastActionAt);
        }
    }

    /// <summary>
    ///     Works out when a phase runs out.
    /// </summary>
    /// <param name="phase">The phase.</param>
    /// <param name="startedAt">When the table was opened.</param>
    /// <param name="lastActionAt">When the table last changed phase or turn.</param>
    /// <returns>The moment the phase runs out.</returns>
    public static DateTime DeadlineOf(BlackjackPhase phase, DateTime startedAt, DateTime lastActionAt)
    {
        return (phase == BlackjackPhase.Joining ? startedAt : lastActionAt) + BlackjackRules.WindowOf(phase);
    }
}

/// <summary>
///     The result of a blackjack move.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Session">The table after the move, or as it stands when the move was refused.</param>
/// <param name="Cost">The extra stake the move needed.</param>
public sealed record BlackjackActionResult(BlackjackActionStatus Status, BlackjackSession? Session, long Cost = 0);

/// <summary>
///     Runs saved blackjack tables: seating, dealing, turns, payouts and the timeouts.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="currency">The currency service the wallets move through.</param>
/// <param name="strings">The localization service.</param>
/// <param name="boostService">The service applying scheduled earning boosts.</param>
/// <param name="dailyChallengeService">The daily challenge service.</param>
/// <param name="client">The Discord client used to keep the table message up to date.</param>
public class BlackjackService(
    IDataConnectionFactory dbFactory,
    ICurrencyService currency,
    GeneratedBotStrings strings,
    EconomyBoostService boostService,
    DailyChallengeService dailyChallengeService,
    DiscordShardedClient client) : INService
{
    private const string GameKey = "blackjack";
    private const int EmbedBudget = 5500;

    /// <summary>
    ///     Gets a table.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="channelId">The channel.</param>
    /// <returns>The table, or null when the channel has none.</returns>
    public async Task<BlackjackSession?> GetAsync(ulong guildId, ulong channelId)
    {
        var row = await LoadAsync(guildId, channelId);
        return row is null ? null : ToSession(row);
    }

    /// <summary>
    ///     Gets the table a player sits at.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="userId">The player.</param>
    /// <returns>The table, or null when the player is not seated anywhere in the guild.</returns>
    public async Task<BlackjackSession?> FindForUserAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var seat = await db.BlackjackSeats.FirstOrDefaultAsync(s => s.GuildId == guildId && s.UserId == userId);

        return seat is null ? null : await GetAsync(guildId, seat.ChannelId);
    }

    /// <summary>
    ///     Opens a table for the player, or seats them at the table that is open in the channel. The bet is taken in
    ///     the same transaction that saves the seat.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="userId">The player.</param>
    /// <param name="bet">The bet.</param>
    /// <returns>What happened, and the table when the player was seated.</returns>
    public async Task<(BlackjackSitStatus Status, BlackjackSession? Session)> SitAsync(ulong guildId,
        ulong channelId, ulong userId, long bet)
    {
        if (bet <= 0)
            return (BlackjackSitStatus.InsufficientFunds, null);

        var change = new GameWalletChange(userId, bet, 0, strings.BetPlacedTransaction(guildId, GameKey), null);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await LoadAsync(guildId, channelId);
            var seatedElsewhere = false;
            var now = DateTime.UtcNow;

            if (row is null)
            {
                var opened = BlackjackRules.Open(userId, bet);
                var created = new BlackjackTableRow
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    Version = 1,
                    StartedAt = now,
                    LastActionAt = now
                };
                Fill(created, opened);

                var openResult = await currency.TryApplyGameStepAsync(guildId, [change], GameKey, async db =>
                {
                    try
                    {
                        await db.InsertAsync(created);
                        await db.InsertAsync(new BlackjackSeatRow
                        {
                            GuildId = guildId, UserId = userId, ChannelId = channelId
                        });
                        return true;
                    }
                    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        seatedElsewhere = ex.TableName == "BlackjackSeats";
                        return false;
                    }
                });

                switch (openResult)
                {
                    case CurrencyStepResult.Applied:
                        await dailyChallengeService.RecordBet(userId, guildId, GameKey, bet);
                        return (BlackjackSitStatus.Opened, ToSession(created));
                    case CurrencyStepResult.InsufficientFunds:
                        return (BlackjackSitStatus.InsufficientFunds, null);
                }

                if (seatedElsewhere)
                    return (BlackjackSitStatus.AlreadySeated, null);

                continue;
            }

            var next = ToSession(row).State;

            if (next.Phase == BlackjackPhase.Joining && IsDue(row, now))
                return (BlackjackSitStatus.InProgress, null);

            switch (BlackjackRules.Join(next, userId, bet))
            {
                case BlackjackJoinResult.Full:
                    return (BlackjackSitStatus.Full, null);
                case BlackjackJoinResult.AlreadySeated:
                    return (BlackjackSitStatus.AlreadySeated, null);
                case BlackjackJoinResult.Closed:
                case BlackjackJoinResult.InvalidBet:
                    return (BlackjackSitStatus.InProgress, null);
            }

            var oldVersion = row.Version;
            var seats = BlackjackRules.EncodeSeats(next.Seats);

            var joinResult = await currency.TryApplyGameStepAsync(guildId, [change], GameKey, async db =>
            {
                var updated = await db.BlackjackTables
                    .Where(t => t.GuildId == guildId && t.ChannelId == channelId && t.Version == oldVersion)
                    .Set(t => t.Seats, seats)
                    .Set(t => t.Version, oldVersion + 1)
                    .Set(t => t.LastActionAt, now)
                    .UpdateAsync();

                if (updated == 0)
                    return false;

                try
                {
                    await db.InsertAsync(new BlackjackSeatRow
                    {
                        GuildId = guildId, UserId = userId, ChannelId = channelId
                    });
                    return true;
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    seatedElsewhere = true;
                    return false;
                }
            });

            switch (joinResult)
            {
                case CurrencyStepResult.Applied:
                    await dailyChallengeService.RecordBet(userId, guildId, GameKey, bet);
                    row.Seats = seats;
                    row.Version = oldVersion + 1;
                    row.LastActionAt = now;
                    return (BlackjackSitStatus.Joined, ToSession(row));
                case CurrencyStepResult.InsufficientFunds:
                    return (BlackjackSitStatus.InsufficientFunds, null);
            }

            if (seatedElsewhere)
                return (BlackjackSitStatus.AlreadySeated, null);
        }

        return (BlackjackSitStatus.InProgress, null);
    }

    /// <summary>
    ///     Remembers which message shows a table, so buttons on any other message are refused.
    /// </summary>
    /// <param name="session">The table.</param>
    /// <param name="messageId">The message showing it.</param>
    public async Task AttachMessageAsync(BlackjackSession session, ulong messageId)
    {
        if (session.Results is not null)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BlackjackTables
            .Where(t => t.GuildId == session.GuildId && t.ChannelId == session.ChannelId && t.MessageId == 0)
            .Set(t => t.MessageId, messageId)
            .UpdateAsync();
    }

    /// <summary>
    ///     Makes a move for a player and, when it ends the round, pays every seat.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="channelId">The channel of the table.</param>
    /// <param name="userId">The player.</param>
    /// <param name="action">The move.</param>
    /// <param name="version">The table version the move was offered for, or null to act on whatever is current.</param>
    /// <param name="messageId">The message the move came from, or null when it did not come from a message.</param>
    /// <returns>What happened and the table afterwards.</returns>
    public async Task<BlackjackActionResult> ActAsync(ulong guildId, ulong channelId, ulong userId,
        BlackjackActions action, int? version = null, ulong? messageId = null)
    {
        var row = await LoadAsync(guildId, channelId);

        if (row is null)
            return new BlackjackActionResult(BlackjackActionStatus.NoGame, null);

        var session = ToSession(row);

        if (session.State.SeatOf(userId) is null)
            return new BlackjackActionResult(BlackjackActionStatus.NotSeated, session);

        if (messageId is { } message && row.MessageId != 0 && row.MessageId != message)
            return new BlackjackActionResult(BlackjackActionStatus.OtherMessage, session);

        if (version is { } expected && expected != row.Version)
            return new BlackjackActionResult(BlackjackActionStatus.Stale, session);

        var next = session.State.Clone();
        var step = BlackjackRules.Apply(next, userId, action, BlackjackRules.DrawCard);

        if (!step.Allowed)
        {
            var wrongTurn = session.State.Phase == BlackjackPhase.Playing && session.State.CurrentSeat?.UserId != userId;
            return new BlackjackActionResult(
                wrongTurn ? BlackjackActionStatus.NotYourTurn : BlackjackActionStatus.NotAllowed, session);
        }

        return await CommitAsync(row, next, userId, step.Cost, false);
    }

    /// <summary>
    ///     Moves a table on after its current phase ran out of time: deals, declines open insurance, or stands the
    ///     player whose turn it is.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="channelId">The channel of the table.</param>
    /// <returns>The table after the step, or null when it is gone, not yet due, or was changed by someone else.</returns>
    public async Task<BlackjackSession?> ExpireAsync(ulong guildId, ulong channelId)
    {
        var row = await LoadAsync(guildId, channelId);

        if (row is null || !IsDue(row, DateTime.UtcNow))
            return null;

        var next = ToSession(row).State.Clone();

        if (!BlackjackRules.Expire(next, BlackjackRules.DrawCard))
            return null;

        var result = await CommitAsync(row, next, null, 0, true);
        return result.Status == BlackjackActionStatus.Applied ? result.Session : null;
    }

    /// <summary>
    ///     Closes a table that never dealt and gives every seat its bet back.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="channelId">The channel of the table.</param>
    /// <returns><see langword="true" /> when the table was closed and refunded.</returns>
    public async Task<bool> AbandonAsync(ulong guildId, ulong channelId)
    {
        var row = await LoadAsync(guildId, channelId);

        if (row is null || (BlackjackPhase)row.Phase != BlackjackPhase.Joining)
            return false;

        var oldVersion = row.Version;
        var changes = ToSession(row).State.Seats
            .Select(s => new GameWalletChange(s.UserId, 0, s.Hands[0].Bet, null,
                strings.BetRefundedTransaction(guildId, GameKey)))
            .ToList();

        var result = await currency.TryApplyGameStepAsync(guildId, changes, GameKey,
            db => DeleteTableAsync(db, guildId, channelId, oldVersion));

        return result == CurrencyStepResult.Applied;
    }

    /// <summary>
    ///     Lists the tables whose current phase has run out of time.
    /// </summary>
    /// <returns>The guild and channel of each.</returns>
    public async Task<IReadOnlyList<(ulong GuildId, ulong ChannelId)>> ListDueAsync()
    {
        var now = DateTime.UtcNow;
        var joinCutoff = now - BlackjackRules.JoinWindow;
        var quickCutoff = now - BlackjackRules.InsuranceWindow;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.BlackjackTables
            .Where(t => t.StartedAt < joinCutoff || t.LastActionAt < quickCutoff)
            .ToListAsync();

        return rows.Where(r => IsDue(r, now)).Select(r => (r.GuildId, r.ChannelId)).ToList();
    }

    /// <summary>
    ///     Builds the embed for a table. Every player's hands are shown, the dealer's second card stays hidden until
    ///     the table is paid out, and the text is shortened when it would not fit.
    /// </summary>
    /// <param name="session">The table.</param>
    /// <param name="emote">The currency emote of the guild.</param>
    /// <returns>The embeds to show.</returns>
    public Embed[] BuildEmbeds(BlackjackSession session, string emote)
    {
        var full = BuildEmbed(session, emote, false);
        return [(full.Length > EmbedBudget ? BuildEmbed(session, emote, true) : full).Build()];
    }

    /// <summary>
    ///     Builds the buttons for the moves the table can take right now.
    /// </summary>
    /// <param name="session">The table.</param>
    /// <returns>The buttons, or an empty set once the table is paid out.</returns>
    public MessageComponent BuildButtons(BlackjackSession session)
    {
        var builder = new ComponentBuilder();

        if (session.Results is not null)
            return builder.Build();

        var guildId = session.GuildId;
        var offered = BlackjackRules.OfferedActions(session.State);

        void Add(BlackjackActions action, string label, ButtonStyle style)
        {
            if ((offered & action) != 0)
                builder.WithButton(label, CustomId(action, session.ChannelId, session.Version), style);
        }

        Add(BlackjackActions.Deal, strings.BlackjackDealButton(guildId), ButtonStyle.Success);
        Add(BlackjackActions.Insurance, strings.BlackjackInsuranceButton(guildId), ButtonStyle.Primary);
        Add(BlackjackActions.DeclineInsurance, strings.BlackjackNoInsuranceButton(guildId), ButtonStyle.Secondary);
        Add(BlackjackActions.Hit, strings.BlackjackHitButton(guildId), ButtonStyle.Primary);
        Add(BlackjackActions.Stand, strings.BlackjackStandButton(guildId), ButtonStyle.Secondary);
        Add(BlackjackActions.Double, strings.BlackjackDoubleButton(guildId), ButtonStyle.Success);
        Add(BlackjackActions.Split, strings.BlackjackSplitButton(guildId), ButtonStyle.Success);
        Add(BlackjackActions.Surrender, strings.BlackjackSurrenderButton(guildId), ButtonStyle.Danger);

        return builder.Build();
    }

    /// <summary>
    ///     Reads the move a button stands for.
    /// </summary>
    /// <param name="name">The move name from the button's custom id.</param>
    /// <returns>The move, or <see cref="BlackjackActions.None" /> when the name is not known.</returns>
    public static BlackjackActions ParseAction(string name)
    {
        return name switch
        {
            "deal" => BlackjackActions.Deal,
            "hit" => BlackjackActions.Hit,
            "stand" => BlackjackActions.Stand,
            "double" => BlackjackActions.Double,
            "split" => BlackjackActions.Split,
            "surrender" => BlackjackActions.Surrender,
            "insurance" => BlackjackActions.Insurance,
            "decline" => BlackjackActions.DeclineInsurance,
            _ => BlackjackActions.None
        };
    }

    /// <summary>
    ///     Edits the table message to show the table as it now stands.
    /// </summary>
    /// <param name="session">The table.</param>
    public async Task UpdateMessageAsync(BlackjackSession session)
    {
        if (session.MessageId == 0 || client.GetChannel(session.ChannelId) is not IMessageChannel channel ||
            await channel.GetMessageAsync(session.MessageId) is not IUserMessage message)
            return;

        var embeds = BuildEmbeds(session, await currency.GetCurrencyEmote(session.GuildId));
        var components = BuildButtons(session);
        await message.ModifyAsync(m =>
        {
            m.Embeds = embeds;
            m.Components = components;
        });
    }

    /// <summary>
    ///     Mentions the player whose turn just started, once.
    /// </summary>
    /// <param name="session">The table after the step.</param>
    public async Task AnnounceTurnAsync(BlackjackSession session)
    {
        if (!session.TurnChanged || session.State.CurrentSeat is not { } seat ||
            client.GetChannel(session.ChannelId) is not IMessageChannel channel)
            return;

        await channel.SendMessageAsync(strings.BlackjackTurnPing(session.GuildId, MentionUtils.MentionUser(seat.UserId)),
            allowedMentions: new AllowedMentions
            {
                UserIds = [seat.UserId]
            });
    }

    private static string CustomId(BlackjackActions action, ulong channelId, int version)
    {
        var name = action switch
        {
            BlackjackActions.Deal => "deal",
            BlackjackActions.Hit => "hit",
            BlackjackActions.Stand => "stand",
            BlackjackActions.Double => "double",
            BlackjackActions.Split => "split",
            BlackjackActions.Surrender => "surrender",
            BlackjackActions.Insurance => "insurance",
            _ => "decline"
        };

        return $"currency_bj:{name}:{channelId}:{version}";
    }

    private static bool IsDue(BlackjackTableRow row, DateTime now)
    {
        return now >= BlackjackSession.DeadlineOf((BlackjackPhase)row.Phase, row.StartedAt, row.LastActionAt);
    }

    private EmbedBuilder BuildEmbed(BlackjackSession session, string emote, bool compact)
    {
        var guildId = session.GuildId;
        var table = session.State;
        var results = session.Results;
        var settled = results is not null;

        var eb = new EmbedBuilder()
            .WithTitle(strings.BlackjackTitle(guildId))
            .WithColor(settled ? Color.Gold : Color.Blue);

        var when = session.Deadline is { } deadline ? $"<t:{new DateTimeOffset(deadline, TimeSpan.Zero).ToUnixTimeSeconds()}:R>" : "";

        eb.WithDescription(table.Phase switch
        {
            BlackjackPhase.Joining => strings.BlackjackJoining(guildId, when, table.Seats.Count,
                BlackjackTable.MaxSeats),
            BlackjackPhase.Insurance => strings.BlackjackInsuranceOffer(guildId, when),
            BlackjackPhase.Playing when table.CurrentSeat is { } current =>
                strings.BlackjackTurnLine(guildId, MentionUtils.MentionUser(current.UserId), when),
            _ => null
        });

        if (table.Dealer.Count > 0)
        {
            var dealer = settled
                ? $"{string.Join(" ", table.Dealer.Select(c => c.Label))}\n{strings.DealerTotal(guildId, results![0].Settlement.DealerTotal)}"
                : $"{table.Dealer[0].Label} {strings.BlackjackHiddenCard(guildId)}";
            eb.AddField(strings.DealerHand(guildId), dealer);
        }

        for (var s = 0; s < table.Seats.Count; s++)
        {
            var seat = table.Seats[s];
            var name = strings.BlackjackSeatName(guildId, s + 1);

            if (!settled && table.Phase == BlackjackPhase.Playing && s == table.Turn)
                name = $"▶ {name}";

            var lines = new List<string>
            {
                MentionUtils.MentionUser(seat.UserId)
            };

            for (var h = 0; h < seat.Hands.Count; h++)
            {
                var hand = seat.Hands[h];
                var prefix = seat.Hands.Count > 1
                    ? $"{(!settled && table.CurrentSeat == seat && h == seat.ActiveHand ? "▶ " : "")}{h + 1}. "
                    : "";

                if (hand.Cards.Count == 0)
                {
                    lines.Add(strings.BlackjackBetLine(guildId, hand.Bet, emote));
                    continue;
                }

                var cards = string.Join(" ", hand.Cards.Select(c => c.Label));
                var line = compact
                    ? strings.BlackjackHandShort(guildId, cards, hand.Total)
                    : strings.BlackjackHandLine(guildId, cards, hand.Total, hand.Bet, emote);

                if (results is not null)
                {
                    var result = results[s].Settlement.Hands[h];
                    line += $" | {(compact ? OutcomeLabel(guildId, result, hand) : strings.BlackjackHandResult(guildId, OutcomeLabel(guildId, result, hand), result.Payout, emote))}";
                }

                lines.Add(prefix + line);
            }

            if (!compact && table.Phase == BlackjackPhase.Insurance && !seat.InsuranceAnswered)
                lines.Add(strings.BlackjackInsurancePending(guildId));
            else if (!compact && results is not null && seat.InsuranceStake > 0)
            {
                var settlement = results[s].Settlement;
                lines.Add(settlement.InsurancePayout > 0
                    ? strings.BlackjackInsurancePaid(guildId, settlement.InsurancePayout - settlement.InsuranceStake,
                        emote)
                    : strings.BlackjackInsuranceLost(guildId, settlement.InsuranceStake, emote));
            }
            else if (!compact && seat.InsuranceStake > 0)
                lines.Add(strings.BlackjackInsuranceLine(guildId, seat.InsuranceStake, emote));

            eb.AddField(name, Clip(string.Join("\n", lines)), false);
        }

        if (session.Expired)
            eb.WithFooter(strings.BlackjackExpired(guildId));

        return eb;
    }

    private static string Clip(string text)
    {
        return text.Length <= 1000 ? text : text[..999] + "…";
    }

    private string OutcomeLabel(ulong guildId, BlackjackHandResult result, BlackjackHand hand)
    {
        return result.Outcome switch
        {
            BlackjackOutcome.Blackjack => strings.BlackjackOutcomeBlackjack(guildId),
            BlackjackOutcome.Surrender => strings.BlackjackOutcomeSurrender(guildId),
            BlackjackOutcome.Win => strings.Won(guildId),
            BlackjackOutcome.Push => strings.Push(guildId),
            _ => hand.IsBust ? strings.BlackjackOutcomeBust(guildId) : strings.Lost(guildId)
        };
    }

    private string Describe(ulong guildId, BlackjackSettlement settlement)
    {
        var profit = settlement.TotalPayout - settlement.TotalStake;

        return profit > 0 ? strings.WonBlackjack(guildId, profit) :
            profit == 0 ? strings.PushBlackjack(guildId) :
            strings.LostBlackjack(guildId, settlement.TotalStake - settlement.TotalPayout);
    }

    private async Task<BlackjackActionResult> CommitAsync(BlackjackTableRow row, BlackjackTable next, ulong? actor,
        long cost, bool expired)
    {
        var guildId = row.GuildId;
        var channelId = row.ChannelId;
        var oldVersion = row.Version;
        var oldPhase = (BlackjackPhase)row.Phase;
        var settled = next.Phase == BlackjackPhase.Settled;
        var results = settled ? await PayAsync(guildId, next) : null;
        var now = DateTime.UtcNow;
        var lastAction = oldPhase == BlackjackPhase.Insurance && next.Phase == BlackjackPhase.Insurance
            ? row.LastActionAt
            : now;

        var changes = new Dictionary<ulong, GameWalletChange>();

        if (actor is { } payer && cost > 0)
        {
            changes[payer] = new GameWalletChange(payer, cost, 0, strings.BetPlacedTransaction(guildId, GameKey), null);
        }

        if (results is not null)
        {
            foreach (var seat in results)
            {
                changes.TryGetValue(seat.UserId, out var existing);
                changes[seat.UserId] = existing with
                {
                    UserId = seat.UserId,
                    Credit = seat.Settlement.TotalPayout,
                    CreditDescription = Describe(guildId, seat.Settlement)
                };
            }
        }

        var update = new BlackjackTableRow();
        Fill(update, next);

        var result = await currency.TryApplyGameStepAsync(guildId, changes.Values.ToList(), GameKey, async db =>
        {
            if (settled)
                return await DeleteTableAsync(db, guildId, channelId, oldVersion);

            return await db.BlackjackTables
                .Where(t => t.GuildId == guildId && t.ChannelId == channelId && t.Version == oldVersion)
                .Set(t => t.Phase, update.Phase)
                .Set(t => t.Seats, update.Seats)
                .Set(t => t.DealerCards, update.DealerCards)
                .Set(t => t.Turn, update.Turn)
                .Set(t => t.Version, oldVersion + 1)
                .Set(t => t.LastActionAt, lastAction)
                .UpdateAsync() > 0;
        });

        switch (result)
        {
            case CurrencyStepResult.StateConflict:
                return new BlackjackActionResult(BlackjackActionStatus.Stale, await GetAsync(guildId, channelId));
            case CurrencyStepResult.InsufficientFunds:
                return new BlackjackActionResult(BlackjackActionStatus.InsufficientFunds, ToSession(row), cost);
        }

        if (results is not null)
            await RecordPayoutsAsync(guildId, results);

        return new BlackjackActionResult(BlackjackActionStatus.Applied, new BlackjackSession
        {
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = row.MessageId,
            Version = settled ? 0 : oldVersion + 1,
            State = next,
            StartedAt = row.StartedAt,
            LastActionAt = lastAction,
            Results = results,
            Expired = expired,
            TurnChanged = !settled && next.Phase == BlackjackPhase.Playing &&
                          (oldPhase != BlackjackPhase.Playing || row.Turn != next.Turn)
        }, cost);
    }

    private static async Task<bool> DeleteTableAsync(MewdekoDb db, ulong guildId, ulong channelId, int version)
    {
        var deleted = await db.BlackjackTables
            .Where(t => t.GuildId == guildId && t.ChannelId == channelId && t.Version == version)
            .DeleteAsync();

        if (deleted == 0)
            return false;

        await db.BlackjackSeats.Where(s => s.GuildId == guildId && s.ChannelId == channelId).DeleteAsync();
        return true;
    }

    private async Task<IReadOnlyList<BlackjackSeatResult>> PayAsync(ulong guildId, BlackjackTable table)
    {
        var paid = new List<BlackjackSeatResult>();

        foreach (var seat in BlackjackRules.Settle(table))
        {
            var raw = seat.Settlement;
            var hands = new List<BlackjackHandResult>();

            foreach (var hand in raw.Hands)
            {
                var payout = hand.Payout > hand.Bet
                    ? await boostService.ApplyToWinningsAsync(guildId, GameKey, hand.Bet, hand.Payout)
                    : hand.Payout;

                hands.Add(hand with
                {
                    Payout = payout
                });
            }

            var insurancePayout = raw.InsurancePayout > raw.InsuranceStake
                ? await boostService.ApplyToWinningsAsync(guildId, GameKey, raw.InsuranceStake, raw.InsurancePayout)
                : raw.InsurancePayout;

            paid.Add(seat with
            {
                Settlement = raw with
                {
                    Hands = hands, InsurancePayout = insurancePayout
                }
            });
        }

        return paid;
    }

    private async Task RecordPayoutsAsync(ulong guildId, IReadOnlyList<BlackjackSeatResult> results)
    {
        foreach (var seat in results)
        {
            foreach (var hand in seat.Settlement.Hands)
                await dailyChallengeService.RecordPayout(seat.UserId, guildId, hand.Bet, hand.Payout);
        }
    }

    private async Task<BlackjackTableRow?> LoadAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BlackjackTables.FirstOrDefaultAsync(t => t.GuildId == guildId && t.ChannelId == channelId);
    }

    private static void Fill(BlackjackTableRow row, BlackjackTable table)
    {
        row.OpenerId = table.OpenerId;
        row.Phase = (short)table.Phase;
        row.Seats = BlackjackRules.EncodeSeats(table.Seats);
        row.DealerCards = BlackjackRules.EncodeCards(table.Dealer);
        row.Turn = table.Turn;
    }

    private static BlackjackSession ToSession(BlackjackTableRow row)
    {
        return new BlackjackSession
        {
            GuildId = row.GuildId,
            ChannelId = row.ChannelId,
            MessageId = row.MessageId,
            Version = row.Version,
            StartedAt = row.StartedAt,
            LastActionAt = row.LastActionAt,
            State = new BlackjackTable
            {
                Phase = (BlackjackPhase)row.Phase,
                OpenerId = row.OpenerId,
                Seats = BlackjackRules.DecodeSeats(row.Seats),
                Dealer = BlackjackRules.DecodeCards(row.DealerCards),
                Turn = row.Turn
            }
        };
    }
}
