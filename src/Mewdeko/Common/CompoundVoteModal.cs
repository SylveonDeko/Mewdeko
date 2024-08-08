namespace Mewdeko.Common;

/// <summary>
/// The votemodal class
/// </summary>
public class CompoundVoteModal
{
    /// <summary>
    /// The vote data
    /// </summary>
    public VoteModel VoteModel { get; set; }
    /// <summary>
    /// The password returned by topgg
    /// </summary>
    public string Password { get; set; }
}