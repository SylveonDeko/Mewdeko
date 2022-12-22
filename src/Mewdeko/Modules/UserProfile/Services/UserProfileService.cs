using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.UserProfile.Services;

public class UserProfileService : INService
{
    private readonly DbService db;
    private readonly HttpClient http;
    private readonly List<string> zodiacList;
    private readonly GamblingConfigService gss;
    public readonly Regex FcRegex = new(@"sw(-\d{4}){3}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public UserProfileService(DbService db, HttpClient http,
        GamblingConfigService gss)
    {
        this.db = db;
        this.http = http;
        this.gss = gss;
        zodiacList = new List<string>
        {
            "Aries",
            "Taurus",
            "Gemini",
            "Cancer",
            "Leo",
            "Virgo",
            "Libra",
            "Scorpio",
            "Sagittarius",
            "Capricorn",
            "Aquarius",
            "Pisces"
        };
    }

    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user?.Pronouns)) return new PronounSearchResult(user.Pronouns, false);
        var result = await http.GetStringAsync(@$"https://pronoundb.org/api/v1/lookup?platform=discord&id={user.UserId}").ConfigureAwait(false);
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

    public async Task<(bool, ZodiacResult)> GetZodiacInfo(ulong discordId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(user.ZodiacSign))
            return (false, null);
        var client = new HttpClient();
        var response = await client.PostAsync($"https://aztro.sameerkumar.website/?sign={user.ZodiacSign.ToLower()}&day=today", null);
        return (true, JsonConvert.DeserializeObject<ZodiacResult>(await response.Content.ReadAsStringAsync()));
    }

    public async Task<string?> GetBio(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.Bio ?? string.Empty;
    }

    public async Task SetBio(IUser user, string bio)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.Bio = bio;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task<DiscordUser.ProfilePrivacyEnum> GetProfilePrivacy(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.ProfilePrivacy;
    }

    public async Task SetPrivacy(IUser user, DiscordUser.ProfilePrivacyEnum privacyEnum)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfilePrivacy = privacyEnum;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task SetBirthdayDisplayMode(IUser user, DiscordUser.BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.BirthdayDisplayMode = birthdayDisplayModeEnum;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task SetBirthday(IUser user, DateTime time)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.Birthday = time;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task<string> GetZodiac(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.ZodiacSign;
    }

    public async Task<bool> SetZodiac(IUser user, string zodiacSign)
    {
        if (!zodiacList.Contains(zodiacSign.ToTitleCase()))
            return false;
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ZodiacSign = zodiacSign.ToTitleCase();
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task SetProfileColor(IUser user, Color color)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfileColor = color.RawValue;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task SetProfileImage(IUser user, string url)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfileImageUrl = url;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    public async Task<bool> SetSwitchFc(IUser user, string fc)
    {
        if (fc.Length != 0 && !FcRegex.IsMatch(fc))
            return false;
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.SwitchFriendCode = fc;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleOptOut(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.StatsOptOut = !dbUser.StatsOptOut;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
        return dbUser.StatsOptOut;
    }

    public async Task<bool> DeleteStatsData(IUser user)
    {
        await using var uow = db.GetDbContext();
        var toRemove = uow.CommandStats.Where(x => x.UserId == user.Id).ToList();
        if (!toRemove.Any())
            return false;
        uow.CommandStats.RemoveRange(toRemove);
        await uow.SaveChangesAsync();
        return true;
    }

    public async Task<Embed?> GetProfileEmbed(IUser user, IUser profileCaller)
    {
        var eb = new EmbedBuilder().WithTitle($"Profile for {user}");
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        if (dbUser.ProfilePrivacy == DiscordUser.ProfilePrivacyEnum.Private && user.Id != profileCaller.Id)
            return null;
        if (dbUser.ProfileColor.HasValue && dbUser.ProfileColor.Value is not 0)
            eb.WithColor(dbUser.ProfileColor.Value);
        else
            eb.WithOkColor();
        eb.WithThumbnailUrl(user.RealAvatarUrl().ToString());
        if (!string.IsNullOrEmpty(dbUser.Bio))
            eb.WithDescription(dbUser.Bio);
        eb.AddField("Currency", $"{dbUser.CurrencyAmount} {gss.Data.Currency.Sign}");
        eb.AddField("Pronouns", (await GetPronounsOrUnspecifiedAsync(user.Id)).Pronouns);
        eb.AddField("Zodiac Sign", string.IsNullOrEmpty(dbUser.ZodiacSign) ? "Unspecified" : dbUser.ZodiacSign);
        if (!string.IsNullOrEmpty(dbUser.ZodiacSign))
            eb.AddField("Horoscope", (await GetZodiacInfo(user.Id)).Item2.Description);
        if (dbUser.Birthday.HasValue)
            switch (dbUser.BirthdayDisplayMode)
            {
                case DiscordUser.BirthdayDisplayModeEnum.Default:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("d"));
                    break;
                case DiscordUser.BirthdayDisplayModeEnum.Disabled:
                    eb.AddField("Birthday", "Private");
                    break;
                case DiscordUser.BirthdayDisplayModeEnum.MonthOnly:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("MMMM"));
                    break;
                case DiscordUser.BirthdayDisplayModeEnum.YearOnly:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("YYYY"));
                    break;
                case DiscordUser.BirthdayDisplayModeEnum.MonthAndDate:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("M"));
                    break;
            }
        else
            eb.AddField("Birthday", "Unspecified");

        eb.AddField("Mutual Bot Servers", (user as SocketUser).MutualGuilds.Count);

        if (!dbUser.SwitchFriendCode.IsNullOrWhiteSpace())
            eb.AddField("Switch Friend Code", dbUser.SwitchFriendCode);

        if (!string.IsNullOrEmpty(dbUser.ProfileImageUrl))
            eb.WithImageUrl(dbUser.ProfileImageUrl);
        return eb.Build();
    }
}