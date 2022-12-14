using System.IO;
using System.Threading.Tasks;

namespace Mewdeko.Extensions;

public static class UserExtensions
{
    public static async Task SendConfirmAsync(this IUser user, string text) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build())
            .ConfigureAwait(false);

    public static async Task<IUserMessage> SendConfirmAsync(this IUser user, string title, string text,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithOkColor().WithDescription(text).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string title, string error,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(error).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);

        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string? error) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build())
            .ConfigureAwait(false);

    public static async Task<IUserMessage> SendFileAsync(this IUser user, string filePath, string? caption = null,
        string? text = null, bool isTts = false)
    {
        var file = File.Open(filePath, FileMode.Open);
        await using var _ = file.ConfigureAwait(false);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(file, caption ?? "x", text, isTts).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendFileAsync(this IUser user, Stream fileStream, string fileName,
        string? caption = null, bool isTts = false) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(fileStream, fileName, caption, isTts).ConfigureAwait(false);

    // This method is used by everything that fetches the avatar from a user
    public static Uri RealAvatarUrl(this IUser usr, ushort size = 2048) =>
        usr.AvatarId == null
            ? new Uri(usr.GetDefaultAvatarUrl())
            : new Uri(usr.GetAvatarUrl(ImageFormat.Auto, size));

    // This method is only used for the xp card
    public static Uri? RealAvatarUrl(this DiscordUser usr) =>
        usr.AvatarId == null
            ? null
            : new Uri(usr.AvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                ? $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.gif?size=2048"
                : $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.png?size=2048");
}