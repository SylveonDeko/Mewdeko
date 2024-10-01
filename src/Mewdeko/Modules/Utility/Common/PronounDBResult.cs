using Newtonsoft.Json;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Represents the result from a pronoun database query.
/// </summary>
public class PronounDbResult
{
    /// <summary>
    ///     Gets or sets the pronouns returned by the query.
    /// </summary>
    [JsonProperty("pronouns")]
    public string Pronouns { get; set; }
}