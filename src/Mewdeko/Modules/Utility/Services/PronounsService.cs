using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility.Services;

public class PronounsService : INService
{
    private readonly DbService _db;
    private readonly HttpClient _http;
    public PronounsService(DbService db, HttpClient http)
    {
        _db = db;
        _http = http;
    }

    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var uow = _db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user?.Pronouns)) return new PronounSearchResult(user.Pronouns, false);
        var result = await _http.GetStringAsync(@$"https://pronoundb.org/api/v1/lookup?platform=discord&id={user.UserId}").ConfigureAwait(false);
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