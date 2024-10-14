using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.UserProfile.Services;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Mewdeko.Modules.UserProfile;

/// <summary>
///     Handles text commands for user profiles, providing functionalities to view and manage user profile details.
/// </summary>
public class UserProfile(DbContextProvider dbProvider) : MewdekoModuleBase<UserProfileService>
{
    /// <summary>
    ///     Shows the user's profile or another user's profile if specified.
    /// </summary>
    /// <param name="user">The user whose profile is to be shown. If null, shows the caller's profile.</param>
    [Cmd]
    [Aliases]
    public async Task Profile(IUser user = null)
    {
        user ??= ctx.User;
        var embed = await Service.GetProfileEmbed(user, ctx.User);
        if (embed is null)
            await ctx.Channel.SendErrorAsync("This user has their profile set to private.", Config);
        else
            await ctx.Channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    /// Allows a user to toggle opting out of greet dms. Only works if the server they are joining uses mewdeko for dm greets.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task GreetDmOptOut()
    {
        var optOut = await Service.ToggleDmGreetOptOutAsync(ctx.User);

        if (optOut)
            await ReplyConfirmLocalizedAsync("greetdm_opt_out");
        else
            await ReplyConfirmLocalizedAsync("greetdm_opt_in");
    }

    /// <summary>
    ///     Sets or updates the biography in the user's profile.
    /// </summary>
    /// <param name="bio">The biography text. Must be under 2048 characters.</param>
    [Cmd]
    [Aliases]
    public async Task SetBio([Remainder] string bio)
    {
        if (bio.Length > 2048)
        {
            await ctx.Channel.SendErrorAsync("Keep it under 2048 characters please,", Config);
            return;
        }

        await Service.SetBio(ctx.User, bio);
        await ctx.Channel.SendConfirmAsync($"Your Profile Bio has been set to:\n{bio}");
    }

    /// <summary>
    ///     Sets the zodiac sign in the user's profile.
    /// </summary>
    /// <param name="zodiac">The zodiac sign to set.</param>
    [Cmd]
    [Aliases]
    public async Task SetZodiac(string zodiac)
    {
        var result = await Service.SetZodiac(ctx.User, zodiac);
        if (!result)
            await ctx.Channel.SendErrorAsync("That zodiac sign doesn't exist.", Config);
        else
            await ctx.Channel.SendConfirmAsync($"Your Zodiac has been set to:\n`{zodiac}`");
    }

    /// <summary>
    ///     Sets the profile color based on an SKColor input.
    /// </summary>
    /// <param name="input">The SKColor representing the desired profile color.</param>
    [Cmd]
    [Aliases]
    public async Task SetProfileColor(SKColor input)
    {
        var discordColor = new Color(input.Red, input.Green, input.Blue);
        await Service.SetProfileColor(ctx.User, discordColor);
        await ctx.Channel.SendConfirmAsync($"Your Profile Color has been set to:\n`{input}`");
    }

    /// <summary>
    ///     Sets the birthday in the user's profile.
    /// </summary>
    /// <param name="dateTime">The birthday date.</param>
    [Cmd]
    [Aliases]
    public async Task SetBirthday([Remainder] DateTime dateTime)
    {
        await Service.SetBirthday(ctx.User, dateTime);
        await ctx.Channel.SendConfirmAsync($"Your birthday has been set to {dateTime:d}");
    }

    /// <summary>
    ///     Toggles the user's opt-out status for command statistics collection.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task UserStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.User);
        if (!optout)
            await ctx.Channel.SendConfirmAsync(
                "Succesfully enabled command stats collection! (This does ***not*** collect message contents!)");
        else
            await ctx.Channel.SendConfirmAsync("Succesfully disable command stats collection.");
    }

    /// <summary>
    ///     Deletes the user's command statistics data.
    /// </summary>
    [Cmd]
    [Aliases]
    [Ratelimit(3600)]
    public async Task DeleteUserStatsData()
    {
        if (await PromptUserConfirmAsync(
                "Are you sure you want to delete your command stats? This action is irreversible!", ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.User))
                await ctx.Channel.SendErrorAsync("Command Stats deleted.", Config);
            else
                await ctx.Channel.SendErrorAsync("There was no data to delete.", Config);
        }
    }

    /// <summary>
    ///     Sets the birthday privacy mode in the user's profile.
    /// </summary>
    /// <param name="birthdayDisplayModeEnum">The birthday display mode to set.</param>
    [Cmd]
    [Aliases]
    public async Task SetBirthdayPrivacy(DiscordUser.BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await Service.SetBirthdayDisplayMode(ctx.User, birthdayDisplayModeEnum);
        await ctx.Channel.SendConfirmAsync(
            $"Your birthday display mode has been set to {birthdayDisplayModeEnum.ToString()}");
    }

    /// <summary>
    ///     Sets the profile image URL in the user's profile.
    /// </summary>
    /// <param name="url">The URL of the image to set as the profile image.</param>
    [Cmd]
    [Aliases]
    public async Task SetProfileImage(string url)
    {
        if (!url.IsImage())
        {
            await ctx.Channel.SendErrorAsync(
                "The image url you provided is invalid. Please make sure it ends with `.gif`, `.png` or `.jpg`",
                Config);
            return;
        }

        await Service.SetProfileImage(ctx.User, url);
        var eb = new EmbedBuilder().WithOkColor().WithDescription("Sucesffully set the profile image to:")
            .WithImageUrl(url);
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Sets the privacy level of the user's profile.
    /// </summary>
    /// <param name="privacyEnum">The privacy setting to apply.</param>
    [Cmd]
    [Aliases]
    public async Task SetPrivacy(DiscordUser.ProfilePrivacyEnum privacyEnum)
    {
        await Service.SetPrivacy(ctx.User, privacyEnum);
        await ctx.Channel.SendConfirmAsync($"Privacy succesfully set to `{privacyEnum.ToString()}`");
    }

    /// <summary>
    ///     Sets or clears the Nintendo Switch friend code in the user's profile.
    /// </summary>
    /// <param name="switchFc">The Nintendo Switch friend code. If blank, clears the existing code.</param>
    [Cmd]
    [Aliases]
    public async Task SetSwitchFc(string switchFc = "")
    {
        if (!await Service.SetSwitchFc(ctx.User, switchFc))
        {
            await ctx.Channel.SendErrorAsync(
                "The Switch Friend Code you provided is invalid. Please make sure it matches the format sw-XXXX-XXXX-XXXX.",
                Config);
            return;
        }


        if (switchFc.Length == 0)
            await ctx.Channel.SendConfirmAsync("Your Switch Friend Code has been removed.");
        else
            await ctx.Channel.SendConfirmAsync($"Your Switch Friend Code has been set to {switchFc}.");
    }

    /// <summary>
    ///     Displays the pronouns of the specified user or the command caller if no user is specified.
    /// </summary>
    /// <param name="user">Optional. The user whose pronouns are to be displayed.</param>
    [Cmd]
    [Aliases]
    public async Task Pronouns(IUser? user = null)
    {
        user ??= ctx.User;
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.GetOrCreateUser(user).ConfigureAwait(false);
        if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
        var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id).ConfigureAwait(false);
        var cb = new ComponentBuilder();
        if (!pronouns.PronounDb)
            cb.WithButton(GetText("pronouns_report_button"), $"pronouns_report.{user.Id};", ButtonStyle.Danger);
        await ctx.Channel.SendConfirmAsync(
            GetText(
                pronouns.PronounDb
                    ? pronouns.Pronouns.Contains(' ') ? "pronouns_pndb_special" : "pronouns_pndb_get"
                    : "pronouns_internal_get", user.ToString(),
                pronouns.Pronouns), cb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets or clears the pronouns for the user.
    /// </summary>
    /// <param name="pronouns">The pronouns to set. If null or empty, clears any existing pronouns.</param>
    [Cmd]
    [Aliases]
    public async Task SetPronouns([Remainder] string? pronouns = null)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var user = await dbContext.GetOrCreateUser(ctx.User).ConfigureAwait(false);
        if (await PronounsDisabled(user).ConfigureAwait(false)) return;
        if (string.IsNullOrWhiteSpace(pronouns))
        {
            var cb = new ComponentBuilder().WithButton(GetText("pronouns_overwrite_button"), "pronouns_overwrite");
            if (string.IsNullOrWhiteSpace(user.Pronouns))
            {
                await ctx.Channel.SendConfirmAsync(GetText("pronouns_internal_no_override"), cb).ConfigureAwait(false);
                return;
            }

            cb.WithButton(GetText("pronouns_overwrite_clear_button"), "pronouns_overwrite_clear", ButtonStyle.Danger);
            await ctx.Channel.SendConfirmAsync(GetText("pronouns_internal_self", user.Pronouns), cb)
                .ConfigureAwait(false);
            return;
        }

        user.Pronouns = pronouns;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
    }

    private async Task<bool> PronounsDisabled(DiscordUser user)
    {
        if (!user.PronounsDisabled) return false;
        await ReplyErrorLocalizedAsync("pronouns_disabled_user", user.PronounsClearedReason).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Force-clears the pronouns for a user, optionally marking them as disabled due to abuse.
    /// </summary>
    /// <param name="user">The user whose pronouns are to be cleared.</param>
    /// <param name="pronounsDisabledAbuse">Whether the pronouns are being disabled due to abuse.</param>
    /// <param name="reason">The reason for the action.</param>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task PronounsForceClear(IUser? user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.GetOrCreateUser(user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Force-clears the pronouns for a user by ID, optionally marking them as disabled due to abuse.
    /// </summary>
    /// <param name="user">The user ID of the user whose pronouns are to be cleared.</param>
    /// <param name="pronounsDisabledAbuse">Whether the pronouns are being disabled due to abuse.</param>
    /// <param name="reason">The reason for the action.</param>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task PronounsForceClear(ulong user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        var dbUser = await dbContext.DiscordUser.AsQueryable().FirstAsync(x => x.UserId == user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared")
            .ConfigureAwait(false);
    }
}