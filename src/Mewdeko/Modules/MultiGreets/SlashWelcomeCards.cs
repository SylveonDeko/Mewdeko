using System.IO;
using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Cards;
using Mewdeko.Modules.MultiGreets.Common;
using Mewdeko.Modules.MultiGreets.Services;

namespace Mewdeko.Modules.MultiGreets;

/// <summary>
///     Slash commands for the image card attached to greet and bye messages.
/// </summary>
[Group("welcomecard", "Set up the image card attached to greet and bye messages")]
/// <param name="presets">The shared background presets.</param>
public class SlashWelcomeCards(CardPresetService presets) : MewdekoSlashModuleBase<WelcomeCardService>
{
    /// <summary>
    ///     Shows the card settings.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    [SlashCommand("show", "Shows the welcome card settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Show(WelcomeCardTarget target)
    {
        var guildId = ctx.Guild.Id;
        var setting = await Service.GetSettingsAsync(guildId, target);
        var summary = WelcomeCardTemplates.Summarize(await Service.GetTemplateAsync(guildId, target));
        await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.WelcomecardShowTitle(guildId, target.ToString()))
            .WithDescription(Strings.WelcomecardShowBody(guildId,
                setting.Enabled ? Strings.WelcomecardOn(guildId) : Strings.WelcomecardOff(guildId),
                summary.Background,
                summary.HasImage ? Strings.WelcomecardImage(guildId) : Strings.WelcomecardNone(guildId),
                summary.Overlay,
                summary.Title,
                string.IsNullOrEmpty(summary.Subtitle) ? Strings.WelcomecardNone(guildId) : summary.Subtitle,
                summary.Font,
                summary.TextColor,
                string.IsNullOrEmpty(summary.Avatar) ? Strings.WelcomecardNone(guildId) : summary.Avatar))
            .Build(), ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Turns the card on or off.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="enabled">Whether the card is attached.</param>
    [SlashCommand("toggle", "Turns the welcome card on or off")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Toggle(WelcomeCardTarget target, bool enabled)
    {
        await Service.SetEnabledAsync(ctx.Guild.Id, target, enabled);
        await ctx.Interaction.SendEphemeralConfirmAsync(enabled
            ? Strings.WelcomecardEnabled(ctx.Guild.Id)
            : Strings.WelcomecardDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the title and subtitle.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="title">The title. Placeholders such as %user.name% work.</param>
    /// <param name="subtitle">The subtitle. Use - for none.</param>
    [SlashCommand("text", "Sets the title and subtitle")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Text(WelcomeCardTarget target, string? title = null, string? subtitle = null) =>
        await ApplyAsync(target, new WelcomeCardBasics
        {
            Title = title,
            Subtitle = subtitle == "-" ? "" : subtitle
        });

    /// <summary>
    ///     Sets the colours, font and avatar shape.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="color">The background colour as hex.</param>
    /// <param name="textColor">The text colour as hex.</param>
    /// <param name="overlay">How dark the overlay on the background is, from 0 to 100.</param>
    /// <param name="font">The font.</param>
    /// <param name="avatar">The avatar shape.</param>
    [SlashCommand("style", "Sets the colours, overlay, font and avatar shape")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Style(WelcomeCardTarget target, string? color = null, string? textColor = null,
        int? overlay = null,
        [Choice("Noto Sans Bold", "noto-bold")] [Choice("Noto Sans", "noto")] [Choice("Uni Sans", "unisans")]
        string? font = null, WelcomeCardAvatarShape? avatar = null) =>
        await ApplyAsync(target, new WelcomeCardBasics
        {
            BackgroundColor = color,
            TextColor = textColor,
            OverlayOpacity = overlay,
            FontName = font,
            AvatarShape = avatar
        });

    /// <summary>
    ///     Lists the background presets: the categories, or the presets in one category.
    /// </summary>
    /// <param name="category">A category id, or nothing to list the categories.</param>
    [SlashCommand("presets", "Lists the shared background presets")]
    [RequireContext(ContextType.Guild)]
    public async Task Presets([Summary("category", "A category id")] string? category = null)
    {
        var guildId = ctx.Guild.Id;
        var embed = CardPresetEmbeds.Build(Strings, guildId, await presets.GetCatalogAsync(), category, "/welcomecard presets");
        if (embed is null)
        {
            await ctx.Interaction.RespondAsync(Strings.CardpresetsUnknownCategory(guildId), ephemeral: true);
            return;
        }

        await ctx.Interaction.RespondAsync(embed: embed, ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Sets or removes the background image.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="image">An image to upload.</param>
    /// <param name="url">A link to an image, or <c>preset:id</c> for a shared background.</param>
    /// <param name="remove">Whether to go back to the plain colour.</param>
    [SlashCommand("background", "Sets or removes the background image")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Background(WelcomeCardTarget target, IAttachment? image = null,
        [Summary("url", "An image link, or preset:id")] string? url = null, bool remove = false)
    {
        await DeferAsync(true);
        var guildId = ctx.Guild.Id;
        if (remove)
        {
            await Service.SetBackgroundAsync(guildId, target, null);
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.WelcomecardBackgroundRemoved(guildId));
            return;
        }

        if (image is null && url?.Trim().StartsWith(CardPresets.Prefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            if (await Service.SetPresetBackgroundAsync(guildId, target, url))
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.WelcomecardSaved(guildId));
            else
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(
                    Strings.WelcomecardBackgroundUnknownPreset(guildId), Config);
            return;
        }

        var source = image?.Url ?? url;
        var bytes = source is null ? null : await Service.DownloadImageAsync(source);
        if (bytes is null || !await Service.SetBackgroundAsync(guildId, target, bytes))
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.WelcomecardBackgroundFailed(guildId), Config);
            return;
        }

        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.WelcomecardSaved(guildId));
    }

    /// <summary>
    ///     Replaces the card's design with a starter layout.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="layout">The starter layout.</param>
    [SlashCommand("reset", "Replaces the card's design with a starter layout")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Reset(WelcomeCardTarget target,
        [Choice("Classic", "classic")] [Choice("Centered", "centered")] [Choice("Banner", "banner")]
        [Choice("Minimal", "minimal")]
        string layout = WelcomeCardTemplates.ClassicId)
    {
        var guildId = ctx.Guild.Id;
        if (await Service.ResetToStarterAsync(guildId, target, layout) == WelcomeCardError.None)
            await ctx.Interaction.SendEphemeralConfirmAsync(Strings.WelcomecardReset(guildId, layout));
        else
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.WelcomecardUnknownStarter(guildId,
                string.Join(", ", WelcomeCardTemplates.Starters.Select(s => s.Id))), Config);
    }

    /// <summary>
    ///     Sends a preview of the card using your own account.
    /// </summary>
    /// <param name="target">Which message the card is for.</param>
    [SlashCommand("preview", "Sends a preview of the welcome card")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Preview(WelcomeCardTarget target)
    {
        await DeferAsync(true);
        var image = await Service.RenderAsync(ctx.Guild.Id, target, ctx.User);
        using var stream = new MemoryStream(image);
        await ctx.Interaction.FollowupWithFileAsync(new FileAttachment(stream, WelcomeCardService.FileName),
            ephemeral: true);
    }

    private async Task ApplyAsync(WelcomeCardTarget target, WelcomeCardBasics basics)
    {
        var guildId = ctx.Guild.Id;
        var message = await Service.ApplyAsync(guildId, target, basics) switch
        {
            WelcomeCardError.None => null,
            WelcomeCardError.InvalidColor => Strings.WelcomecardInvalidColor(guildId),
            WelcomeCardError.InvalidOpacity => Strings.WelcomecardInvalidOpacity(guildId),
            WelcomeCardError.InvalidFont => Strings.WelcomecardInvalidFont(guildId,
                string.Join(", ", WelcomeCardTemplates.Fonts.Keys)),
            WelcomeCardError.MissingElement => Strings.WelcomecardMissingElement(guildId),
            _ => Strings.WelcomecardInvalidText(guildId)
        };

        if (message is null)
            await ctx.Interaction.SendEphemeralConfirmAsync(Strings.WelcomecardSaved(guildId));
        else
            await ctx.Interaction.SendEphemeralErrorAsync(message, Config);
    }
}
