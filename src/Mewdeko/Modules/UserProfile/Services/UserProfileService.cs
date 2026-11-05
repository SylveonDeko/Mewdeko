using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Birthday;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Services.Strings;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.UserProfile.Services;

/// <summary>
///     Provides services for managing user profiles, including operations such as retrieving and setting user pronouns,
///     zodiac information, biographies, and privacy settings. It interacts with external services for some
///     functionalities, like fetching pronouns from PronounDB.
/// </summary>
public partial class UserProfileService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly Regex fcRegex = MyRegex();
    private readonly HttpClient http;
    private readonly GeneratedBotStrings Strings;
    private readonly List<string> zodiacList;


    /// <summary>
    ///     Initializes a new instance of the <see cref="UserProfileService" /> class with specified database and HTTP
    ///     services.
    /// </summary>
    /// <param name="dbFactory">The database service for accessing user data.</param>
    /// <param name="http">The HTTP client used for making external API calls.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="eventHandler">Publishes birthday changes to other services.</param>
    public UserProfileService(IDataConnectionFactory dbFactory, HttpClient http, GeneratedBotStrings strings,
        EventHandler eventHandler)
    {
        this.eventHandler = eventHandler;
        this.dbFactory = dbFactory;
        this.http = http;
        Strings = strings;
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
    ///     Toggles a users ability to receive greet dms. Only viable if the server they are joining uses mewdeko for greet
    ///     dms.
    /// </summary>
    /// <param name="user">The user who whishes to toggle the setting</param>
    /// <returns>The opposite of the current state.</returns>
    public async Task<bool> ToggleDmGreetOptOutAsync(IUser? user)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var dUser = await db.GetOrCreateUser(user);
        dUser.GreetDmsOptOut = !dUser.GreetDmsOptOut;
        await db.UpdateAsync(dUser);
        return dUser.GreetDmsOptOut;
    }

    /// <summary>
    ///     Asynchronously retrieves the pronouns for a given Discord user ID, either from the local database or PronounDB if
    ///     unspecified.
    /// </summary>
    /// <param name="discordId">The Discord user ID to retrieve pronouns for.</param>
    /// <returns>A <see cref="PronounSearchResult" /> object containing the pronouns or a default value if unspecified.</returns>
    /// <remarks>
    ///     This method first attempts to find the user's pronouns in the local database. If not found or unspecified, it then
    ///     queries PronounDB's API v2.
    /// </remarks>
    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var user = await dbContext.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == discordId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(user?.Pronouns)) return new PronounSearchResult(user.Pronouns, false);

        try
        {
            // Set up the HTTP request with proper User-Agent header as required by PronounDB v2
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://pronoundb.org/api/v2/lookup?platform=discord&ids={user.UserId}");
            request.Headers.Add("User-Agent", "Mewdeko/1.0 (Discord Bot)");

            var response = await http.SendAsync(request).ConfigureAwait(false);
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var pronounData = JsonSerializer.Deserialize<Dictionary<string, PronounDbV2User>>(result);

            // Check if the user exists in the response
            if (pronounData != null && pronounData.TryGetValue(user.UserId.ToString(), out var userData))
            {
                // Get English pronouns (default locale)
                if (userData.Sets.TryGetValue("en", out var pronouns) && pronouns.Length > 0)
                {
                    var pronounString = string.Join("/", pronouns);
                    return new PronounSearchResult(pronounString switch
                    {
                        "he" => "he/him",
                        "she" => "she/her",
                        "they" => "they/them",
                        "it" => "it/its",
                        "any" => "Any pronouns",
                        "ask" => "Ask me my pronouns",
                        "avoid" => "Avoid pronouns, use my name",
                        "other" => "Other pronouns",
                        _ => pronounString // Use the actual pronoun combination if it's custom
                    }, true);
                }
            }
        }
        catch (Exception)
        {
            // If API call fails, fall back to unspecified
        }

        return new PronounSearchResult("Unspecified", true);
    }

    /// <summary>
    ///     Asynchronously retrieves the biography of a given user.
    /// </summary>
    /// <param name="user">The user to retrieve the biography for.</param>
    /// <returns>The biography as a string, or an empty string if not set.</returns>
    public async Task<string?> GetBio(IUser? user)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        return dbUser.Bio ?? string.Empty;
    }

    /// <summary>
    ///     Asynchronously sets the biography for a given user.
    /// </summary>
    /// <param name="user">The user to set the biography for.</param>
    /// <param name="bio">The biography text to set.</param>
    public async Task SetBio(IUser? user, string bio)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.Bio = bio;
        await dbContext.UpdateAsync(dbUser);
    }

    /// <summary>
    ///     Asynchronously gets the privacy setting of a user's profile.
    /// </summary>
    /// <param name="user">The user to retrieve the privacy setting for.</param>
    /// <returns>The current privacy setting of the user's profile.</returns>
    public async Task<ProfilePrivacyEnum> GetProfilePrivacy(IUser? user)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        return (ProfilePrivacyEnum)dbUser.ProfilePrivacy;
    }


    /// <summary>
    ///     Asynchronously sets the privacy setting for a given user's profile.
    /// </summary>
    /// <param name="user">The user to set the privacy for.</param>
    /// <param name="privacyEnum">The privacy setting to apply to the user's profile.</param>
    public async Task SetPrivacy(IUser? user, ProfilePrivacyEnum privacyEnum)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.ProfilePrivacy = (int)privacyEnum;
        await dbContext.UpdateAsync(dbUser);
    }

    /// <summary>
    ///     Asynchronously sets the display mode for a user's birthday.
    /// </summary>
    /// <param name="user">The user to set the birthday display mode for.</param>
    /// <param name="birthdayDisplayModeEnum">The birthday display mode to set.</param>
    public async Task SetBirthdayDisplayMode(IUser? user, BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.BirthdayDisplayMode = (int)birthdayDisplayModeEnum;
        await dbContext.UpdateAsync(dbUser);
    }

    /// <summary>
    ///     Asynchronously sets the birthday for a given user.
    /// </summary>
    /// <param name="user">The user to set the birthday for.</param>
    /// <param name="time">The birthday date to set.</param>
    public async Task SetBirthday(IUser? user, DateTime time)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.Birthday = time;
        await dbContext.UpdateAsync(dbUser);
        if (user is not null)
            _ = eventHandler.PublishEventAsync("BirthdaySet", new MemberActivityEventArgs(0, user.Id));
    }

    /// <summary>
    ///     Asynchronously retrieves the zodiac sign of a given user.
    /// </summary>
    /// <param name="user">The user to retrieve the zodiac sign for.</param>
    /// <returns>The zodiac sign of the user.</returns>
    public async Task<string> GetZodiac(IUser? user)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        return dbUser.ZodiacSign;
    }

    /// <summary>
    ///     Asynchronously sets the zodiac sign for a given user.
    /// </summary>
    /// <param name="user">The user to set the zodiac sign for.</param>
    /// <param name="zodiacSign">The zodiac sign to set.</param>
    /// <returns>True if the zodiac sign was successfully set; otherwise, false.</returns>
    /// <remarks>
    ///     The method checks if the provided zodiac sign is within a predefined list of valid signs before setting it.
    /// </remarks>
    public async Task<bool> SetZodiac(IUser? user, string zodiacSign)
    {
        if (!zodiacList.Contains(zodiacSign.ToTitleCase()))
            return false;
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.ZodiacSign = zodiacSign.ToTitleCase();
        await dbContext.UpdateAsync(dbUser);

        return true;
    }

    /// <summary>
    ///     Asynchronously sets the profile color for a given user.
    /// </summary>
    /// <param name="user">The user to set the profile color for.</param>
    /// <param name="color">The color to set as the user's profile color.</param>
    public async Task SetProfileColor(IUser? user, Color color)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.ProfileColor = color.RawValue;
        await dbContext.UpdateAsync(dbUser);
    }

    /// <summary>
    ///     Asynchronously sets the profile image URL for a given user.
    /// </summary>
    /// <param name="user">The user to set the profile image for.</param>
    /// <param name="url">The URL of the image to set as the user's profile image.</param>
    public async Task SetProfileImage(IUser? user, string url)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.ProfileImageUrl = url;
        await dbContext.UpdateAsync(dbUser);
    }

    /// <summary>
    ///     Asynchronously sets the Nintendo Switch friend code for a given user.
    /// </summary>
    /// <param name="user">The user to set the friend code for.</param>
    /// <param name="fc">The friend code to set.</param>
    /// <returns>True if the friend code was successfully set; otherwise, false.</returns>
    /// <remarks>
    ///     Validates the friend code format before setting it. Friend codes must match the pattern "SW-XXXX-XXXX-XXXX".
    /// </remarks>
    public async Task<bool> SetSwitchFc(IUser? user, string fc)
    {
        if (fc.Length != 0 && !fcRegex.IsMatch(fc))
            return false;
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.SwitchFriendCode = fc;
        await dbContext.UpdateAsync(dbUser);

        return true;
    }

    /// <summary>
    ///     Toggles the opt-out setting for a user's participation in statistics gathering.
    /// </summary>
    /// <param name="user">The user to toggle the opt-out setting for.</param>
    /// <returns>True if the user is now opted out; otherwise, false.</returns>
    public async Task<bool> ToggleOptOut(IUser? user)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        dbUser.StatsOptOut = !dbUser.StatsOptOut;
        await dbContext.UpdateAsync(dbUser);

        return dbUser.StatsOptOut;
    }

    /// <summary>
    ///     Asynchronously deletes statistical data for a given user.
    /// </summary>
    /// <param name="user">The user to delete statistical data for.</param>
    /// <returns>True if data was found and deleted; otherwise, false.</returns>
    public async Task<bool> DeleteStatsData(IUser user)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toRemove = dbContext.CommandStats.Where(x => x.UserId == user.Id);
        if (!toRemove.Any())
            return false;
        await toRemove.DeleteAsync();

        return true;
    }

    /// <summary>
    ///     Asynchronously generates an embed for displaying a user's profile information.
    /// </summary>
    /// <param name="user">The user to generate the profile embed for.</param>
    /// <param name="profileCaller">The user requesting the profile view, for privacy considerations.</param>
    /// <returns>
    ///     An <see cref="Embed" /> object containing the user's profile information, or null if privacy settings prevent
    ///     displaying it.
    /// </returns>
    /// <remarks>
    ///     The profile embed includes information such as pronouns, zodiac sign, birthday, and other personalization settings,
    ///     considering the privacy settings.
    /// </remarks>
    public async Task<Embed?> GetProfileEmbed(IUser? user, IUser profileCaller)
    {
        var eb = new EmbedBuilder().WithTitle(Strings.ProfileForUser(0, user));
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var dbUser = await dbContext.GetOrCreateUser(user);
        if ((ProfilePrivacyEnum)dbUser.ProfilePrivacy == ProfilePrivacyEnum.Private && user.Id != profileCaller.Id)
            return null;
        if (dbUser.ProfileColor.HasValue && dbUser.ProfileColor.Value is not 0)
            eb.WithColor((Color)dbUser.ProfileColor.Value);
        else
            eb.WithOkColor();
        eb.WithThumbnailUrl(user.RealAvatarUrl().ToString());
        if (!string.IsNullOrEmpty(dbUser.Bio))
            eb.WithDescription(dbUser.Bio);
        eb.AddField("Pronouns", (await GetPronounsOrUnspecifiedAsync(user.Id)).Pronouns);
        eb.AddField("Zodiac Sign", string.IsNullOrEmpty(dbUser.ZodiacSign) ? "Unspecified" : dbUser.ZodiacSign);
        if (dbUser.Birthday.HasValue)
            switch ((BirthdayDisplayModeEnum)dbUser.BirthdayDisplayMode)
            {
                case BirthdayDisplayModeEnum.Default:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("d"));
                    break;
                case BirthdayDisplayModeEnum.Disabled:
                    eb.AddField("Birthday", "Private");
                    break;
                case BirthdayDisplayModeEnum.MonthOnly:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("MMMM"));
                    break;
                case BirthdayDisplayModeEnum.YearOnly:
                    eb.AddField("Birthday", dbUser.Birthday.Value.ToString("YYYY"));
                    break;
                case BirthdayDisplayModeEnum.MonthAndDate:
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