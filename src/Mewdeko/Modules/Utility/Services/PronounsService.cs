using System.Net.Http;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service for fetching and managing user pronouns from an external API.
/// </summary>
public class PronounsService(DbService db, HttpClient http) : INService
{
    /// <summary>
    /// Asynchronously gets a user's pronouns from the local database or queries an external API if not found.
    /// </summary>
    /// <param name="discordId">The Discord user ID to fetch pronouns for.</param>
    /// <returns>A PronounSearchResult object containing the pronouns or indicating if unspecified.</returns>
    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user?.Pronouns)) return new PronounSearchResult(user.Pronouns, false);
        var result = await http.GetStringAsync($"https://pronoundb.org/api/v1/lookup?platform=discord&id={user.UserId}")
            .ConfigureAwait(false);
        var pronouns = JsonConvert.DeserializeObject<PronounDbResult>(result);
        return new PronounSearchResult((pronouns?.Pronouns ?? "unspecified") switch
        {
            "unspecified" => "Unspecified",
            "hh" => "he/him",
            "hi" => "he/it",
            "hs" => "he/she",
            "ht" => "he/they",
            "ih" => "it/him",
            "ii" => "it/its",
            "is" => "it/she",
            "it" => "it/they",
            "shh" => "she/he",
            "sh" => "she/her",
            "si" => "she/it",
            "st" => "she/they",
            "th" => "they/he",
            "ti" => "they/it",
            "ts" => "they/she",
            "tt" => "they/them",
            "any" => "Any pronouns",
            "other" => "Pronouns not on PronounDB",
            "ask" => "Pronouns you should ask them about",
            "avoid" => "A name instead of pronouns",
            _ => "Failed to resolve pronouns."
        }, true);
    }
}