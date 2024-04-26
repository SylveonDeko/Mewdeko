using System.Net.Http;
using System.Text.RegularExpressions;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.UserProfile.Services;

/// <summary>
/// Provides services for managing user profiles, including operations such as retrieving and setting user pronouns, zodiac information, biographies, and privacy settings. It interacts with external services for some functionalities, like fetching pronouns from PronounDB.
/// </summary>
public partial class UserProfileService : INService
{
    private readonly DbService db;
    private readonly Regex fcRegex = MyRegex();
    private readonly HttpClient http;
    private readonly List<string> zodiacList;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileService"/> class with specified database and HTTP services.
    /// </summary>
    /// <param name="db">The database service for accessing user data.</param>
    /// <param name="http">The HTTP client used for making external API calls.</param>
    public UserProfileService(DbService db, HttpClient http)
    {
        this.db = db;
        this.http = http;
        zodiacList =
        [
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
        ];
    }

    /// <summary>
    /// Asynchronously retrieves the pronouns for a given Discord user ID, either from the local database or PronounDB if unspecified.
    /// </summary>
    /// <param name="discordId">The Discord user ID to retrieve pronouns for.</param>
    /// <returns>A <see cref="PronounSearchResult"/> object containing the pronouns or a default value if unspecified.</returns>
    /// <remarks>
    /// This method first attempts to find the user's pronouns in the local database. If not found or unspecified, it then queries PronounDB's API.
    /// </remarks>
    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user?.Pronouns)) return new PronounSearchResult(user.Pronouns, false);
        var result = await http.GetStringAsync($"https://pronoundb.org/api/v1/lookup?platform=discord&id={user.UserId}")
            .ConfigureAwait(false);
        var pronouns = JsonConvert.DeserializeObject<PronounDbResult>(result);
        // if (pronouns.Pronouns != "unspecified")
        // {
        //     user.PndbCache = pronouns.Pronouns;
        //     await uow.SaveChangesAsync();
        // }

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


    /// <summary>
    /// Asynchronously fetches zodiac information for a given Discord user ID.
    /// </summary>
    /// <param name="discordId">The Discord user ID to retrieve zodiac information for.</param>
    /// <returns>A tuple containing a boolean indicating success, and a <see cref="ZodiacResult"/> object if successful.</returns>
    /// <remarks>
    /// The zodiac information is retrieved from an external API and requires the user's zodiac sign to be previously set.
    /// </remarks>
    public async Task<(bool, ZodiacResult)> GetZodiacInfo(ulong discordId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.DiscordUser.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(user.ZodiacSign))
            return (false, null);
        var client = new HttpClient();
        var response =
            await client.PostAsync($"https://aztro.sameerkumar.website/?sign={user.ZodiacSign.ToLower()}&day=today",
                null);
        return (true, JsonConvert.DeserializeObject<ZodiacResult>(await response.Content.ReadAsStringAsync()));
    }

    /// <summary>
    /// Asynchronously retrieves the biography of a given user.
    /// </summary>
    /// <param name="user">The user to retrieve the biography for.</param>
    /// <returns>The biography as a string, or an empty string if not set.</returns>
    public async Task<string?> GetBio(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.Bio ?? string.Empty;
    }

    /// <summary>
    /// Asynchronously sets the biography for a given user.
    /// </summary>
    /// <param name="user">The user to set the biography for.</param>
    /// <param name="bio">The biography text to set.</param>
    public async Task SetBio(IUser user, string bio)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.Bio = bio;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously gets the privacy setting of a user's profile.
    /// </summary>
    /// <param name="user">The user to retrieve the privacy setting for.</param>
    /// <returns>The current privacy setting of the user's profile.</returns>
    public async Task<DiscordUser.ProfilePrivacyEnum> GetProfilePrivacy(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.ProfilePrivacy;
    }


    /// <summary>
    /// Asynchronously sets the privacy setting for a given user's profile.
    /// </summary>
    /// <param name="user">The user to set the privacy for.</param>
    /// <param name="privacyEnum">The privacy setting to apply to the user's profile.</param>
    public async Task SetPrivacy(IUser user, DiscordUser.ProfilePrivacyEnum privacyEnum)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfilePrivacy = privacyEnum;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously sets the display mode for a user's birthday.
    /// </summary>
    /// <param name="user">The user to set the birthday display mode for.</param>
    /// <param name="birthdayDisplayModeEnum">The birthday display mode to set.</param>
    public async Task SetBirthdayDisplayMode(IUser user, DiscordUser.BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.BirthdayDisplayMode = birthdayDisplayModeEnum;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously sets the birthday for a given user.
    /// </summary>
    /// <param name="user">The user to set the birthday for.</param>
    /// <param name="time">The birthday date to set.</param>
    public async Task SetBirthday(IUser user, DateTime time)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.Birthday = time;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously retrieves the zodiac sign of a given user.
    /// </summary>
    /// <param name="user">The user to retrieve the zodiac sign for.</param>
    /// <returns>The zodiac sign of the user.</returns>
    public async Task<string> GetZodiac(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        return dbUser.ZodiacSign;
    }

    /// <summary>
    /// Asynchronously sets the zodiac sign for a given user.
    /// </summary>
    /// <param name="user">The user to set the zodiac sign for.</param>
    /// <param name="zodiacSign">The zodiac sign to set.</param>
    /// <returns>True if the zodiac sign was successfully set; otherwise, false.</returns>
    /// <remarks>
    /// The method checks if the provided zodiac sign is within a predefined list of valid signs before setting it.
    /// </remarks>
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

    /// <summary>
    /// Asynchronously sets the profile color for a given user.
    /// </summary>
    /// <param name="user">The user to set the profile color for.</param>
    /// <param name="color">The color to set as the user's profile color.</param>
    public async Task SetProfileColor(IUser user, Color color)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfileColor = color.RawValue;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously sets the profile image URL for a given user.
    /// </summary>
    /// <param name="user">The user to set the profile image for.</param>
    /// <param name="url">The URL of the image to set as the user's profile image.</param>
    public async Task SetProfileImage(IUser user, string url)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.ProfileImageUrl = url;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Asynchronously sets the Nintendo Switch friend code for a given user.
    /// </summary>
    /// <param name="user">The user to set the friend code for.</param>
    /// <param name="fc">The friend code to set.</param>
    /// <returns>True if the friend code was successfully set; otherwise, false.</returns>
    /// <remarks>
    /// Validates the friend code format before setting it. Friend codes must match the pattern "SW-XXXX-XXXX-XXXX".
    /// </remarks>
    public async Task<bool> SetSwitchFc(IUser user, string fc)
    {
        if (fc.Length != 0 && !fcRegex.IsMatch(fc))
            return false;
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.SwitchFriendCode = fc;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Toggles the opt-out setting for a user's participation in statistics gathering.
    /// </summary>
    /// <param name="user">The user to toggle the opt-out setting for.</param>
    /// <returns>True if the user is now opted out; otherwise, false.</returns>
    public async Task<bool> ToggleOptOut(IUser user)
    {
        await using var uow = db.GetDbContext();
        var dbUser = await uow.GetOrCreateUser(user);
        dbUser.StatsOptOut = !dbUser.StatsOptOut;
        uow.DiscordUser.Update(dbUser);
        await uow.SaveChangesAsync();
        return dbUser.StatsOptOut;
    }

    /// <summary>
    /// Asynchronously deletes statistical data for a given user.
    /// </summary>
    /// <param name="user">The user to delete statistical data for.</param>
    /// <returns>True if data was found and deleted; otherwise, false.</returns>
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

    /// <summary>
    /// Asynchronously generates an embed for displaying a user's profile information.
    /// </summary>
    /// <param name="user">The user to generate the profile embed for.</param>
    /// <param name="profileCaller">The user requesting the profile view, for privacy considerations.</param>
    /// <returns>An <see cref="Embed"/> object containing the user's profile information, or null if privacy settings prevent displaying it.</returns>
    /// <remarks>
    /// The profile embed includes information such as pronouns, zodiac sign, birthday, and other personalization settings, considering the privacy settings.
    /// </remarks>
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

    [GeneratedRegex("sw(-\\d{4}){3}", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}