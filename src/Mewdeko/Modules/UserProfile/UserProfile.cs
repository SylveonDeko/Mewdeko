using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.UserProfile.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

namespace Mewdeko.Modules.UserProfile;

public class UserProfile : MewdekoModuleBase<UserProfileService>
{
    private readonly DbService db;

    public UserProfile(DbService db) => this.db = db;

    [Cmd, Aliases]
    public async Task Profile(IUser user = null)
    {
        user ??= ctx.User;
        var embed = await Service.GetProfileEmbed(user, ctx.User);
        if (embed is null)
            await ctx.Channel.SendErrorAsync("This user has their profile set to private.");
        else
            await ctx.Channel.SendMessageAsync(embed: embed);
    }

    [Cmd, Aliases]
    public async Task SetBio([Remainder] string bio)
    {
        if (bio.Length > 2048)
        {
            await ctx.Channel.SendErrorAsync("Keep it under 2048 characters please,");
            return;
        }

        await Service.SetBio(ctx.User, bio);
        await ctx.Channel.SendConfirmAsync($"Your Profile Bio has been set to:\n{bio}");
    }

    [Cmd, Aliases]
    public async Task SetZodiac(string zodiac)
    {
        var result = await Service.SetZodiac(ctx.User, zodiac);
        if (!result)
            await ctx.Channel.SendErrorAsync("That zodiac sign doesn't exist.");
        else
            await ctx.Channel.SendConfirmAsync($"Your Zodiac has been set to:\n`{zodiac}`");
    }

    [Cmd, Aliases]
    public async Task SetProfileColor(Color input)
    {
        var color = Rgba32.ParseHex(input.ToHex());
        var discordColor = new Discord.Color(color.R, color.G, color.B);
        await Service.SetProfileColor(ctx.User, discordColor);
        await ctx.Channel.SendConfirmAsync($"Your Profile Color has been set to:\n`{color}`");
    }

    [Cmd, Aliases]
    public async Task SetBirthday([Remainder] DateTime dateTime)
    {
        await Service.SetBirthday(ctx.User, dateTime);
        await ctx.Channel.SendConfirmAsync($"Your birthday has been set to {dateTime:d}");
    }

    [Cmd, Aliases]
    public async Task UserStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.User);
        if (!optout)
            await ctx.Channel.SendConfirmAsync("Succesfully enabled command stats collection! (This does ***not*** collect message contents!)");
        else
            await ctx.Channel.SendConfirmAsync("Succesfully disable command stats collection.");
    }

    [Cmd, Aliases, Ratelimit(3600)]
    public async Task DeleteUserStatsData()
    {
        if (await PromptUserConfirmAsync("Are you sure you want to delete your command stats? This action is irreversible!", ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.User))
                await ctx.Channel.SendErrorAsync("Command Stats deleted.");
            else
                await ctx.Channel.SendErrorAsync("There was no data to delete.");
        }
    }

    [Cmd, Aliases]
    public async Task SetBirthdayPrivacy(DiscordUser.BirthdayDisplayModeEnum birthdayDisplayModeEnum)
    {
        await Service.SetBirthdayDisplayMode(ctx.User, birthdayDisplayModeEnum);
        await ctx.Channel.SendConfirmAsync($"Your birthday display mode has been set to {birthdayDisplayModeEnum.ToString()}");
    }

    [Cmd, Aliases]
    public async Task SetProfileImage(string url)
    {
        if (!url.IsImage())
        {
            await ctx.Channel.SendErrorAsync("The image url you provided is invalid. Please make sure it ends with `.gif`, `.png` or `.jpg`");
            return;
        }

        await Service.SetProfileImage(ctx.User, url);
        var eb = new EmbedBuilder().WithOkColor().WithDescription("Sucesffully set the profile image to:").WithImageUrl(url);
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    [Cmd, Aliases]
    public async Task SetPrivacy(DiscordUser.ProfilePrivacyEnum privacyEnum)
    {
        await Service.SetPrivacy(ctx.User, privacyEnum);
        await ctx.Channel.SendConfirmAsync($"Privacy succesfully set to `{privacyEnum.ToString()}`");
    }

    [Cmd, Aliases]
    public async Task SetSwitchFc(string switchFc = "")
    {
        if (!await Service.SetSwitchFc(ctx.User, switchFc))
        {
            await ctx.Channel.SendErrorAsync("The Switch Friend Code you provided is invalid. Please make sure it matches the format sw-XXXX-XXXX-XXXX.");
            return;
        }


        if (switchFc.Length == 0)
            await ctx.Channel.SendConfirmAsync("Your Switch Friend Code has been removed.");
        else
            await ctx.Channel.SendConfirmAsync($"Your Switch Friend Code has been set to {switchFc}.");
    }

    [Cmd, Aliases]
    public async Task Pronouns(IUser? user = null)
    {
        user ??= ctx.User;
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var dbUser = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        if (await PronounsDisabled(dbUser).ConfigureAwait(false)) return;
        var pronouns = await Service.GetPronounsOrUnspecifiedAsync(user.Id).ConfigureAwait(false);
        var cb = new ComponentBuilder();
        if (!pronouns.PronounDb)
            cb.WithButton(GetText("pronouns_report_button"), $"pronouns_report.{user.Id};", ButtonStyle.Danger);
        await ctx.Channel.SendConfirmAsync(
            GetText(pronouns.PronounDb ? pronouns.Pronouns.Contains(' ') ? "pronouns_pndb_special" : "pronouns_pndb_get" : "pronouns_internal_get", user.ToString(),
                pronouns.Pronouns), cb).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task SetPronouns([Remainder] string? pronouns = null)
    {
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var user = await uow.GetOrCreateUser(ctx.User).ConfigureAwait(false);
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
            await ctx.Channel.SendConfirmAsync(GetText("pronouns_internal_self", user.Pronouns), cb).ConfigureAwait(false);
            return;
        }

        user.Pronouns = pronouns;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync("pronouns_internal_update", user.Pronouns).ConfigureAwait(false);
    }

    private async Task<bool> PronounsDisabled(DiscordUser user)
    {
        if (!user.PronounsDisabled) return false;
        await ReplyErrorLocalizedAsync("pronouns_disabled_user", user.PronounsClearedReason).ConfigureAwait(false);
        return true;
    }


    [Cmd, Aliases, OwnerOnly]
    public async Task PronounsForceClear(IUser? user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var dbUser = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared").ConfigureAwait(false);
    }

    [Cmd, Aliases, OwnerOnly]
    public async Task PronounsForceClear(ulong user, bool pronounsDisabledAbuse, [Remainder] string reason)
    {
        var uow = db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        var dbUser = await uow.DiscordUser.AsQueryable().FirstAsync(x => x.UserId == user).ConfigureAwait(false);
        dbUser.PronounsDisabled = pronounsDisabledAbuse;
        dbUser.PronounsClearedReason = reason;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await ConfirmLocalizedAsync(pronounsDisabledAbuse ? "pronouns_disabled_user" : "pronouns_cleared").ConfigureAwait(false);
    }
}