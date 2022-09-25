using Discord.Commands;
using JikanDotNet;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.UserProfile.Services;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using Color = SixLabors.ImageSharp.Color;

namespace Mewdeko.Modules.UserProfile;

public partial class UserProfile : MewdekoModuleBase<UserProfileService>
{
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
    public async Task SetBio([Remainder]string bio)
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
    public async Task SetBirthday([Remainder]DateTime dateTime)
    {
        await Service.SetBirthday(ctx.User, dateTime);
        await ctx.Channel.SendConfirmAsync($"Your birthday has been set to {dateTime:d}");
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
}