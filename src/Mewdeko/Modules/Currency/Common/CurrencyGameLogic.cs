using SkiaSharp;

namespace Mewdeko.Modules.Currency.Common;

/// <summary>
///     Pure game logic shared by the slash currency games. Mirrors the helpers in the text module so
///     both entry points produce the same outcomes.
/// </summary>
public static class CurrencyGameLogic
{
    /// <summary>
    ///     The symbols on the slot machine reels.
    /// </summary>
    public static readonly string[] SlotSymbols = ["🍒", "🍊", "🍋", "🍇", "💎", "7️⃣"];

    /// <summary>
    ///     The colour symbols used by the memory game.
    /// </summary>
    public static readonly string[] MemoryEmojis =
    [
        "🔴", "🟡", "🟢", "🔵", "🟣", "🟠", "⚫", "⚪"
    ];

    /// <summary>
    ///     Picks a segment index using the supplied weights.
    /// </summary>
    /// <param name="segmentCount">Number of segments.</param>
    /// <param name="weights">Weight array for each segment.</param>
    /// <returns>The selected segment index.</returns>
    public static int GenerateWeightedRandomSegment(int segmentCount, int[] weights)
    {
        var totalWeight = weights.Sum();
        var randomValue = CurrencyRng.Next(totalWeight);

        var currentWeight = 0;
        for (var i = 0; i < segmentCount; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
                return i;
        }

        return segmentCount - 1;
    }

    /// <summary>
    ///     Computes the balance change for a spin wheel segment label such as "-10%" or "+$30".
    /// </summary>
    /// <param name="segment">The segment label.</param>
    /// <param name="amount">The bet amount.</param>
    /// <returns>The signed change to apply.</returns>
    public static long ComputeSegmentDelta(string segment, long amount)
    {
        if (segment.EndsWith('%'))
        {
            var percent = int.Parse(segment[1..^1]);
            var portion = (long)Math.Ceiling(amount * (percent / 100.0));
            return segment.StartsWith('-') ? -portion : portion;
        }

        var val = int.Parse(segment.Replace("$", "").Replace("+", "").Replace("-", ""));
        return segment.StartsWith('-') ? -val : val;
    }

    /// <summary>
    ///     Draws the spin wheel, highlighting the winning segment.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the wheel.</param>
    /// <param name="numSegments">The number of segments in the wheel.</param>
    /// <param name="segments">An array containing the labels for each segment.</param>
    /// <param name="winningSegment">The index of the winning segment.</param>
    public static void DrawWheel(SKCanvas canvas, int numSegments, string[] segments, int winningSegment)
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

            var startAngle = i * 360 / numSegments - offsetAngle;
            var sweepAngle = 360f / numSegments;

            canvas.DrawArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                startAngle, sweepAngle, true, paint);
        }

        using var textFont = new SKFont();
        textFont.Size = 20;

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.Black;
        textPaint.IsAntialias = true;

        for (var i = 0; i < numSegments; i++)
        {
            var startAngle = i * 360 / numSegments - offsetAngle;
            var middleAngle = startAngle + 360 / numSegments / 2;

            var textX = centerX + radius * 0.7f * (float)Math.Cos(DegreesToRadians(middleAngle));
            var textY = centerY + radius * 0.7f * (float)Math.Sin(DegreesToRadians(middleAngle)) + textFont.Size / 2;

            canvas.DrawText(segments[i], textX, textY, SKTextAlign.Center, textFont, textPaint);
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

        var arrowPathBuilder = new SKPathBuilder();
        arrowPathBuilder.MoveTo(centerX, centerY);
        arrowPathBuilder.LineTo(arrowShaftEnd.X, arrowShaftEnd.Y);

        arrowPathBuilder.MoveTo(arrowTip.X, arrowTip.Y);
        arrowPathBuilder.LineTo(arrowLeftSide.X, arrowLeftSide.Y);
        arrowPathBuilder.LineTo(arrowRightSide.X, arrowRightSide.Y);
        arrowPathBuilder.LineTo(arrowTip.X, arrowTip.Y);

        using var arrowPath = arrowPathBuilder.Detach();
        canvas.DrawPath(arrowPath, arrowPaint);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * (float)Math.PI / 180;
    }

    private static SKColor GeneratePastelColor()
    {
        var hue = (float)CurrencyRng.Next(0, 361);
        var saturation = 40f + (float)CurrencyRng.NextDouble() * 20f;
        var lightness = 70f + (float)CurrencyRng.NextDouble() * 20f;

        return SKColor.FromHsl(hue, saturation, lightness);
    }

    /// <summary>
    ///     Calculates the slot machine winnings for a set of symbols.
    /// </summary>
    /// <param name="result">The three drawn symbols.</param>
    /// <param name="bet">The bet amount.</param>
    /// <returns>The winnings, or zero for no match.</returns>
    public static long CalculateSlotWinnings(string[] result, long bet)
    {
        if (result[0] == result[1] && result[1] == result[2])
        {
            return result[0] switch
            {
                "💎" => bet * 10,
                "7️⃣" => bet * 7,
                "🍇" => bet * 5,
                _ => bet * 3
            };
        }

        if (result[0] == result[1] || result[1] == result[2] || result[0] == result[2])
        {
            return bet * 2;
        }

        return 0;
    }

    /// <summary>
    ///     Draws a random playing card.
    /// </summary>
    /// <returns>The rank, suit and comparison value of the card.</returns>
    public static (string rank, string suit, int value) GenerateCard()
    {
        var suits = new[]
        {
            "♠️", "♥️", "♦️", "♣️"
        };
        var ranks = new[]
        {
            ("A", 14), ("2", 2), ("3", 3), ("4", 4), ("5", 5), ("6", 6), ("7", 7), ("8", 8), ("9", 9), ("10", 10),
            ("J", 11), ("Q", 12), ("K", 13)
        };

        var suit = suits[CurrencyRng.Next(suits.Length)];
        var rank = ranks[CurrencyRng.Next(ranks.Length)];

        return (rank.Item1, suit, rank.Item2);
    }

    /// <summary>
    ///     Rolls until the craps point is made or a seven is rolled.
    /// </summary>
    /// <param name="point">The established point.</param>
    /// <returns>Whether the point was made and a description of the deciding roll.</returns>
    public static (bool won, string description) RollForPoint(int point)
    {
        var attempts = 0;
        while (attempts < 10)
        {
            var die1 = CurrencyRng.Next(1, 7);
            var die2 = CurrencyRng.Next(1, 7);
            var total = die1 + die2;
            attempts++;

            if (total == point)
                return (true, $"Rolled {die1} + {die2} = {total} (Point made!)");
            if (total == 7)
                return (false, $"Rolled {die1} + {die2} = 7 (Seven out!)");
        }

        return (false, "Seven out!");
    }

    /// <summary>
    ///     Produces a scratch card symbol row.
    /// </summary>
    /// <param name="winning">Whether the row should be a matching set.</param>
    /// <returns>The three symbols.</returns>
    public static string GenerateScratchSymbols(bool winning)
    {
        var symbols = new[]
        {
            "🍒", "🍋", "🍊", "🍇", "⭐", "💎", "🔔", "🍀"
        };

        if (winning)
        {
            var winSymbol = symbols[CurrencyRng.Next(symbols.Length)];
            return $"{winSymbol} {winSymbol} {winSymbol}";
        }

        var symbol1 = symbols[CurrencyRng.Next(symbols.Length)];
        var symbol2 = symbols[CurrencyRng.Next(symbols.Length)];
        var symbol3 = symbols[CurrencyRng.Next(symbols.Length)];

        while (symbol1 == symbol2 && symbol2 == symbol3)
        {
            symbol2 = symbols[CurrencyRng.Next(symbols.Length)];
            symbol3 = symbols[CurrencyRng.Next(symbols.Length)];
        }

        return $"{symbol1} {symbol2} {symbol3}";
    }

    /// <summary>
    ///     Calculates a baccarat hand total.
    /// </summary>
    /// <param name="hand">The hand.</param>
    /// <returns>The total modulo ten.</returns>
    public static int CalculateBaccaratTotal(List<(string rank, string suit, int value)> hand)
    {
        var total = 0;
        foreach (var card in hand)
        {
            var cardValue = card.rank switch
            {
                "A" => 1,
                "J" or "Q" or "K" => 0,
                _ => int.Parse(card.rank) > 10 ? 0 : int.Parse(card.rank)
            };
            total += cardValue;
        }

        return total % 10;
    }

    /// <summary>
    ///     Generates the multiplier at which a crash round ends.
    /// </summary>
    /// <returns>The crash point.</returns>
    public static double GenerateCrashPoint()
    {
        var roll = CurrencyRng.NextDouble();
        return roll switch
        {
            < 0.5 => 1.0 + CurrencyRng.NextDouble() * 0.5,
            < 0.8 => 1.5 + CurrencyRng.NextDouble() * 1.5,
            < 0.95 => 3.0 + CurrencyRng.NextDouble() * 2.0,
            _ => 5.0 + CurrencyRng.NextDouble() * 5.0
        };
    }

    /// <summary>
    ///     Parses a keno number selection.
    /// </summary>
    /// <param name="input">Numbers separated by spaces, commas or semicolons.</param>
    /// <returns>The distinct numbers found.</returns>
    public static List<int> ParseKenoNumbers(string input)
    {
        var numbers = new HashSet<int>();
        var parts = input.Split([
            ' ', ',', ';'
        ], StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers.ToList();
    }

    /// <summary>
    ///     Draws the keno numbers.
    /// </summary>
    /// <param name="count">How many numbers to draw.</param>
    /// <returns>The drawn numbers.</returns>
    public static HashSet<int> GenerateKenoNumbers(int count)
    {
        var numbers = new HashSet<int>();
        while (numbers.Count < count)
        {
            numbers.Add(CurrencyRng.Next(1, 81));
        }

        return numbers;
    }

    /// <summary>
    ///     Looks up the keno payout multiplier.
    /// </summary>
    /// <param name="picked">How many numbers were picked.</param>
    /// <param name="matches">How many matched.</param>
    /// <returns>The multiplier, or zero.</returns>
    public static double CalculateKenoMultiplier(int picked, int matches)
    {
        return (picked, matches) switch
        {
            (1, 1) => 3.0,
            (2, 2) => 12.0,
            (3, 2) => 1.0,
            (3, 3) => 42.0,
            (4, 2) => 0.5,
            (4, 3) => 5.0,
            (4, 4) => 100.0,
            (5, 3) => 2.0,
            (5, 4) => 20.0,
            (5, 5) => 800.0,
            _ => 0.0
        };
    }

    /// <summary>
    ///     Simulates a plinko ball falling through the pegs.
    /// </summary>
    /// <param name="rows">The number of peg rows.</param>
    /// <returns>The column the ball occupied at each row.</returns>
    public static List<int> SimulatePlinkoBall(int rows)
    {
        var path = new List<int>
        {
            rows / 2
        };

        for (var i = 0; i < rows; i++)
        {
            var currentPos = path.Last();
            var direction = CurrencyRng.Next(2) == 0 ? -1 : 1;
            var newPos = Math.Max(0, Math.Min(rows, currentPos + direction));
            path.Add(newPos);
        }

        return path;
    }

    /// <summary>
    ///     Looks up the plinko multiplier for a landing slot.
    /// </summary>
    /// <param name="rows">The number of peg rows.</param>
    /// <param name="slot">The landing slot.</param>
    /// <returns>The multiplier.</returns>
    public static double GetPlinkoMultiplier(int rows, int slot)
    {
        var center = rows / 2;
        var distance = Math.Abs(slot - center);

        return distance switch
        {
            0 => 0.5,
            1 => 1.2,
            2 => 2.0,
            3 => 5.0,
            4 => 10.0,
            _ => 0.1
        };
    }

    /// <summary>
    ///     Draws the plinko board and the ball's path.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="rows">The number of peg rows.</param>
    /// <param name="path">The ball's path.</param>
    public static void DrawPlinkoBoard(SKCanvas canvas, int rows, List<int> path)
    {
        canvas.Clear(SKColors.White);

        using var pegPaint = new SKPaint
        {
            Color = SKColors.Gray, IsAntialias = true
        };
        using var pathPaint = new SKPaint
        {
            Color = SKColors.Red, IsAntialias = true, StrokeWidth = 3, Style = SKPaintStyle.Stroke
        };

        var width = canvas.LocalClipBounds.Width;
        var height = canvas.LocalClipBounds.Height;
        var pegSpacing = width / (rows + 1);
        var rowSpacing = height / (rows + 2);

        for (var row = 0; row <= rows; row++)
        {
            for (var col = 0; col <= row; col++)
            {
                var x = width / 2 + (col - row / 2.0f) * pegSpacing;
                var y = rowSpacing * (row + 1);
                canvas.DrawCircle(x, y, 5, pegPaint);
            }
        }

        for (var i = 0; i < path.Count - 1; i++)
        {
            var x1 = width / 2 + (path[i] - rows / 2.0f) * pegSpacing;
            var y1 = rowSpacing * (i + 1);
            var x2 = width / 2 + (path[i + 1] - rows / 2.0f) * pegSpacing;
            var y2 = rowSpacing * (i + 2);

            canvas.DrawLine(x1, y1, x2, y2, pathPaint);
        }
    }

    /// <summary>
    ///     Gets the wheel of fortune segment layouts.
    /// </summary>
    /// <returns>Segment labels and weights keyed by wheel type.</returns>
    public static Dictionary<string, (string[] segments, int[] weights)> GetWheelConfigurations()
    {
        return new Dictionary<string, (string[] segments, int[] weights)>
        {
            {
                "classic", ([
                    "x0.5", "x1.2", "x2.0", "x0.8", "x1.5", "x0.1"
                ], [
                    3, 2, 1, 2, 1, 1
                ])
            },
            {
                "risky", ([
                    "x0.1", "x10.0", "x0.2", "x5.0", "x0.1", "x20.0"
                ], [
                    4, 1, 3, 1, 4, 1
                ])
            },
            {
                "balanced", ([
                    "x0.8", "x1.5", "x1.0", "x2.0", "x1.2", "x0.9"
                ], [
                    2, 2, 2, 1, 2, 1
                ])
            }
        };
    }

    /// <summary>
    ///     Draws the wheel of fortune with the winning segment in gold.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="segments">The segment labels.</param>
    /// <param name="winningIndex">The winning segment index.</param>
    public static void DrawWheelFortune(SKCanvas canvas, string[] segments, int winningIndex)
    {
        canvas.Clear(SKColors.White);

        var centerX = canvas.LocalClipBounds.MidX;
        var centerY = canvas.LocalClipBounds.MidY;
        var radius = Math.Min(centerX, centerY) - 10;

        var colors = new[]
        {
            SKColors.Red, SKColors.Blue, SKColors.Green, SKColors.Yellow, SKColors.Purple, SKColors.Orange
        };

        for (var i = 0; i < segments.Length; i++)
        {
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = i == winningIndex ? SKColors.Gold : colors[i % colors.Length],
                IsAntialias = true
            };

            var startAngle = i * 360f / segments.Length;
            var sweepAngle = 360f / segments.Length;

            canvas.DrawArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                startAngle, sweepAngle, true, paint);
        }

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black, IsAntialias = true
        };
        using var font = new SKFont
        {
            Size = 16
        };

        for (var i = 0; i < segments.Length; i++)
        {
            var angle = (i + 0.5f) * 360f / segments.Length;
            var radians = angle * Math.PI / 180;
            var textX = centerX + radius * 0.7f * (float)Math.Cos(radians);
            var textY = centerY + radius * 0.7f * (float)Math.Sin(radians);

            canvas.DrawText(segments[i], textX, textY, SKTextAlign.Center, font, textPaint);
        }
    }

    /// <summary>
    ///     Computes the balance change for a wheel of fortune segment.
    /// </summary>
    /// <param name="segment">The segment label such as "x2.0".</param>
    /// <param name="betAmount">The bet amount.</param>
    /// <returns>The signed change to apply.</returns>
    public static long ComputeWheelBalanceChange(string segment, long betAmount)
    {
        if (segment.StartsWith("x"))
        {
            var multiplier = double.Parse(segment[1..]);
            return (long)(betAmount * multiplier) - betAmount;
        }

        return -betAmount;
    }

    /// <summary>
    ///     Generates the duck lineup for a race.
    /// </summary>
    /// <param name="count">The number of ducks.</param>
    /// <returns>The duck symbols.</returns>
    public static List<string> GenerateDucks(int count)
    {
        var ducks = new List<string>();
        var duckEmojis = new[]
        {
            "🦆", "🐥", "🐤", "🐣", "🦢"
        };

        for (var i = 0; i < count; i++)
        {
            ducks.Add(duckEmojis[i % duckEmojis.Length]);
        }

        return ducks;
    }

    /// <summary>
    ///     Simulates a duck race.
    /// </summary>
    /// <param name="ducks">The ducks racing.</param>
    /// <returns>The finishing order with times.</returns>
    public static List<(int duckIndex, double time)> SimulateDuckRace(List<string> ducks)
    {
        var results = new List<(int duckIndex, double time)>();

        for (var i = 0; i < ducks.Count; i++)
        {
            var time = 10.0 + CurrencyRng.NextDouble() * 5.0;
            results.Add((i, time));
        }

        return results.OrderBy(r => r.time).ToList();
    }

    /// <summary>
    ///     Generates a bingo card.
    /// </summary>
    /// <param name="size">The card edge length.</param>
    /// <returns>The card numbers.</returns>
    public static int[,] GenerateBingoCard(int size)
    {
        var card = new int[size, size];
        var used = new HashSet<int>();

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                int number;
                do
                {
                    number = CurrencyRng.Next(1, 76);
                } while (used.Contains(number));

                used.Add(number);
                card[i, j] = number;
            }
        }

        return card;
    }

    /// <summary>
    ///     Calls bingo numbers.
    /// </summary>
    /// <param name="count">How many to call.</param>
    /// <returns>The called numbers.</returns>
    public static List<int> CallBingoNumbers(int count)
    {
        var numbers = new HashSet<int>();
        while (numbers.Count < count)
        {
            numbers.Add(CurrencyRng.Next(1, 76));
        }

        return numbers.ToList();
    }

    /// <summary>
    ///     Checks whether a bingo card has a completed line.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="calledNumbers">The called numbers.</param>
    /// <param name="forceWin">Whether the round is a forced win.</param>
    /// <returns>Whether the card won.</returns>
    public static bool CheckBingoWin(int[,] card, List<int> calledNumbers, bool forceWin)
    {
        if (forceWin) return true;

        var size = card.GetLength(0);
        var calledSet = new HashSet<int>(calledNumbers);

        for (var i = 0; i < size; i++)
        {
            var rowComplete = true;
            var colComplete = true;

            for (var j = 0; j < size; j++)
            {
                if (!calledSet.Contains(card[i, j])) rowComplete = false;
                if (!calledSet.Contains(card[j, i])) colComplete = false;
            }

            if (rowComplete || colComplete) return true;
        }

        var diag1Complete = true;
        var diag2Complete = true;
        for (var i = 0; i < size; i++)
        {
            if (!calledSet.Contains(card[i, i])) diag1Complete = false;
            if (!calledSet.Contains(card[i, size - 1 - i])) diag2Complete = false;
        }

        return diag1Complete || diag2Complete;
    }

    /// <summary>
    ///     Formats a bingo card for display.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="calledNumbers">The called numbers.</param>
    /// <returns>The formatted card.</returns>
    public static string FormatBingoCard(int[,] card, List<int> calledNumbers)
    {
        var size = card.GetLength(0);
        var calledSet = new HashSet<int>(calledNumbers);
        var result = "";

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                var number = card[i, j];
                var marker = calledSet.Contains(number) ? "✅" : "❌";
                result += $"{number:D2}{marker} ";
            }

            result += "\n";
        }

        return result;
    }

    /// <summary>
    ///     Generates a memory sequence.
    /// </summary>
    /// <param name="length">The sequence length.</param>
    /// <returns>Symbol indices.</returns>
    public static List<int> GenerateMemorySequence(int length)
    {
        var sequence = new List<int>();
        for (var i = 0; i < length; i++)
        {
            sequence.Add(CurrencyRng.Next(0, 8));
        }

        return sequence;
    }

    /// <summary>
    ///     Parses the user's memory answer into symbol indices.
    /// </summary>
    /// <param name="input">The user's message.</param>
    /// <param name="emojis">The symbol set.</param>
    /// <returns>Symbol indices.</returns>
    public static List<int> ParseMemorySequence(string input, string[] emojis)
    {
        var sequence = new List<int>();
        var emojiToIndex = emojis.Select((emoji, index) => new
            {
                emoji, index
            })
            .ToDictionary(x => x.emoji, x => x.index);

        foreach (var emoji in emojis)
        {
            var count = input.Count(c => c.ToString() == emoji);
            for (var i = 0; i < count; i++)
            {
                if (emojiToIndex.TryGetValue(emoji, out var index))
                {
                    sequence.Add(index);
                }
            }
        }

        return sequence;
    }
}