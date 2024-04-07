namespace Mewdeko.Modules.UserProfile.Common;

/// <summary>
/// Represents the result of a pronoun search.
/// </summary>
/// <param name="Pronouns">The pronouns found in the search.</param>
/// <param name="PronounDb">Whether the pronouns were found in the database.</param>
public record PronounSearchResult(string Pronouns, bool PronounDb);