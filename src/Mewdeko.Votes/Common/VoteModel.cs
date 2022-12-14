namespace Mewdeko.Votes.Common;

public class VoteModel
{
    /// <summary>
    /// Discord ID of the bot that received a vote.
    /// </summary>
    public string Bot { get; set; }

    /// <summary>
    /// Server ID if Bot is not populated.
    /// </summary>
    public string Guild { get; set; }

    /// <summary>
    /// Discord ID of the user who voted.
    /// </summary>
    public string User { get; set; }

    /// <summary>
    /// The type of the vote (should always be "upvote" except when using the test button it's "test").
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Whether the weekend multiplier is in effect, meaning users votes count as two.
    /// </summary>
    public bool Weekend { get; set; } = false;

    /// <summary>
    /// Query string params found on the /bot/:ID/vote page. Example: ?a=2.
    /// </summary>
    public string Query { get; set; }
}