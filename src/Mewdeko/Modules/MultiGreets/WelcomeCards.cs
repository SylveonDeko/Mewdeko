using System.IO;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Cards;
using Mewdeko.Modules.MultiGreets.Common;
using Mewdeko.Modules.MultiGreets.Services;

namespace Mewdeko.Modules.MultiGreets;

/// <summary>
///     Commands for the image card attached to greet and bye messages.
/// </summary>
/// <param name="guildSettings">The guild settings service, for the prefix.</param>
/// <param name="presets">The shared background presets.</param>
public class WelcomeCards(GuildSettingsService guildSettings, CardPresetService presets)
    : MewdekoModuleBase<WelcomeCardService>
{
    /// <summary>
    ///     Shows or changes the welcome card.
    /// </summary>
    /// <param name="target">greet, bye, dmgreet or boost.</param>
    /// <param name="option">
    ///     show, enable, disable, color, background, overlay, title, subtitle, font, textcolor, avatar or reset.
    /// </param>
    /// <param name="value">The value for the option.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WelcomeCard(WelcomeCardTarget target, string option = "show", [Remainder] string? value = null)
    {
        var guildId = ctx.Guild.Id;
        var prefix = $"{await guildSettings.GetPrefix(ctx.Guild)}welcomecard";
        value = value?.Trim();

        switch (option.ToLowerInvariant())
        {
            case "show":
                await ShowAsync(target);
                return;
            case "enable":
            case "on":
                await (await Service.SetPlacementAsync(guildId, target, CardPlacement.Attached)
                    ? ReplyConfirmAsync(Strings.WelcomecardEnabled(guildId))
                    : ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId)));
                return;
            case "disable":
            case "off":
                await (await Service.SetPlacementAsync(guildId, target, CardPlacement.None)
                    ? ReplyConfirmAsync(Strings.WelcomecardDisabled(guildId))
                    : ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId)));
                return;
            case "placement":
                await PlacementAsync(target, value);
                return;
            case "background":
                await BackgroundAsync(target, value);
                return;
            case "reset":
                await ResetAsync(target, string.IsNullOrWhiteSpace(value) ? WelcomeCardTemplates.ClassicId : value);
                return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            await ReplyErrorAsync(Strings.WelcomecardMissingArgument(guildId, prefix));
            return;
        }

        switch (option.ToLowerInvariant())
        {
            case "color":
                await ApplyAsync(target, new WelcomeCardBasics
                {
                    BackgroundColor = value
                });
                break;
            case "textcolor":
                await ApplyAsync(target, new WelcomeCardBasics
                {
                    TextColor = value
                });
                break;
            case "overlay":
                if (!int.TryParse(value.TrimEnd('%'), out var opacity))
                {
                    await ReplyErrorAsync(Strings.WelcomecardInvalidOpacity(guildId));
                    return;
                }

                await ApplyAsync(target, new WelcomeCardBasics
                {
                    OverlayOpacity = opacity
                });
                break;
            case "title":
                await ApplyAsync(target, new WelcomeCardBasics
                {
                    Title = value
                });
                break;
            case "subtitle":
                await ApplyAsync(target, new WelcomeCardBasics
                {
                    Subtitle = value == "-" ? "" : value
                });
                break;
            case "font":
                await ApplyAsync(target, new WelcomeCardBasics
                {
                    FontName = value.ToLowerInvariant()
                });
                break;
            case "avatar":
                if (!Enum.TryParse<WelcomeCardAvatarShape>(value, true, out var shape))
                {
                    await ReplyErrorAsync(Strings.WelcomecardInvalidShape(guildId));
                    return;
                }

                await ApplyAsync(target, new WelcomeCardBasics
                {
                    AvatarShape = shape
                });
                break;
            default:
                await ReplyErrorAsync(Strings.WelcomecardMissingArgument(guildId, prefix));
                break;
        }
    }

    /// <summary>
    ///     Sends a preview of the welcome card using your own account.
    /// </summary>
    /// <param name="target">greet, bye, dmgreet or boost.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WelcomeCardPreview(WelcomeCardTarget target = WelcomeCardTarget.Greet)
    {
        var owner = (await Service.OwnersAsync(ctx.Guild.Id, target)).FirstOrDefault(-1);
        if (owner < 0)
        {
            await ReplyErrorAsync(Strings.WelcomecardNoMessage(ctx.Guild.Id));
            return;
        }

        using var typing = ctx.Channel.EnterTypingState();
        var image = await Service.RenderAsync(ctx.Guild.Id, target, owner, ctx.User);
        await ctx.Channel.SendFileAsync(new MemoryStream(image), WelcomeCardService.FileName);
    }

    /// <summary>
    ///     Lists the background presets: the categories, or the presets in one category.
    /// </summary>
    /// <param name="category">A category id, or nothing to list the categories.</param>
    /// <example>.backgroundpresets</example>
    /// <example>.backgroundpresets nature</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task BackgroundPresets(string? category = null)
    {
        var prefix = $"{await guildSettings.GetPrefix(ctx.Guild)}backgroundpresets";
        var embed = CardPresetEmbeds.Build(Strings, ctx.Guild.Id, await presets.GetCatalogAsync(), category, prefix);
        if (embed is null)
        {
            await ReplyErrorAsync(Strings.CardpresetsUnknownCategory(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
    }

    private async Task PlacementAsync(WelcomeCardTarget target, string? value)
    {
        var guildId = ctx.Guild.Id;
        var placement = value?.ToLowerInvariant() switch
        {
            "none" => CardPlacement.None,
            "embed" => CardPlacement.EmbedImage,
            "attached" => CardPlacement.Attached,
            _ => (CardPlacement?)null
        };
        if (placement is not { } chosen)
        {
            await ReplyErrorAsync(Strings.WelcomecardInvalidPlacement(guildId));
            return;
        }

        if (!await Service.SetPlacementAsync(guildId, target, chosen))
        {
            await ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId));
            return;
        }

        await ReplyConfirmAsync(Strings.WelcomecardPlacementSaved(guildId, PlacementName(guildId, chosen)));
    }

    private string PlacementName(ulong guildId, CardPlacement placement)
    {
        return placement switch
        {
            CardPlacement.EmbedImage => Strings.WelcomecardPlacementEmbed(guildId),
            CardPlacement.Attached => Strings.WelcomecardPlacementAttached(guildId),
            _ => Strings.WelcomecardPlacementNone(guildId)
        };
    }

    private async Task BackgroundAsync(WelcomeCardTarget target, string? value)
    {
        var guildId = ctx.Guild.Id;
        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            await (await Service.SetBackgroundAsync(guildId, target, null)
                ? ReplyConfirmAsync(Strings.WelcomecardBackgroundRemoved(guildId))
                : ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId)));
            return;
        }

        if (value?.StartsWith(CardPresets.Prefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            await (await Service.SetPresetBackgroundAsync(guildId, target, value)
                ? ReplyConfirmAsync(Strings.WelcomecardSaved(guildId))
                : ReplyErrorAsync(Strings.WelcomecardBackgroundUnknownPreset(guildId)));
            return;
        }

        var url = string.IsNullOrWhiteSpace(value) ? ctx.Message.Attachments.FirstOrDefault()?.Url : value;
        var bytes = url is null ? null : await Service.DownloadImageAsync(url);
        if (bytes is null || !await Service.SetBackgroundAsync(guildId, target, bytes))
        {
            await ReplyErrorAsync(Strings.WelcomecardBackgroundFailed(guildId));
            return;
        }

        await ReplyConfirmAsync(Strings.WelcomecardSaved(guildId));
    }

    private async Task ResetAsync(WelcomeCardTarget target, string starter)
    {
        var guildId = ctx.Guild.Id;
        if (await Service.ResetToStarterAsync(guildId, target, starter) == WelcomeCardError.None)
        {
            await ReplyConfirmAsync(Strings.WelcomecardReset(guildId, starter.ToLowerInvariant()));
            return;
        }

        await ReplyErrorAsync(await Service.OwnersAsync(guildId, target) is { Count: 0 }
            ? Strings.WelcomecardNoMessage(guildId)
            : Strings.WelcomecardUnknownStarter(guildId,
                string.Join(", ", WelcomeCardTemplates.Starters.Select(s => s.Id))));
    }

    private async Task ApplyAsync(WelcomeCardTarget target, WelcomeCardBasics basics)
    {
        var guildId = ctx.Guild.Id;
        switch (await Service.ApplyAsync(guildId, target, basics))
        {
            case WelcomeCardError.None:
                await ReplyConfirmAsync(Strings.WelcomecardSaved(guildId));
                break;
            case WelcomeCardError.InvalidColor:
                await ReplyErrorAsync(Strings.WelcomecardInvalidColor(guildId));
                break;
            case WelcomeCardError.InvalidOpacity:
                await ReplyErrorAsync(Strings.WelcomecardInvalidOpacity(guildId));
                break;
            case WelcomeCardError.InvalidFont:
                await ReplyErrorAsync(Strings.WelcomecardInvalidFont(guildId,
                    string.Join(", ", WelcomeCardTemplates.Fonts.Keys)));
                break;
            case WelcomeCardError.MissingElement:
                await ReplyErrorAsync(Strings.WelcomecardMissingElement(guildId));
                break;
            case WelcomeCardError.NoMessage:
                await ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId));
                break;
            default:
                await ReplyErrorAsync(Strings.WelcomecardInvalidText(guildId));
                break;
        }
    }

    private async Task ShowAsync(WelcomeCardTarget target)
    {
        var guildId = ctx.Guild.Id;
        var owner = (await Service.OwnersAsync(guildId, target)).FirstOrDefault(-1);
        if (owner < 0)
        {
            await ReplyErrorAsync(Strings.WelcomecardNoMessage(guildId));
            return;
        }

        var setting = await Service.GetCardAsync(guildId, target, owner);
        var summary = WelcomeCardTemplates.Summarize(await Service.GetTemplateAsync(guildId, target, owner));
        await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.WelcomecardShowTitle(guildId, target.ToString()))
            .WithDescription(Strings.WelcomecardShowBody(guildId,
                PlacementName(guildId, setting.Placement),
                summary.Background,
                summary.HasImage ? Strings.WelcomecardImage(guildId) : Strings.WelcomecardNone(guildId),
                summary.Overlay,
                summary.Title,
                string.IsNullOrEmpty(summary.Subtitle) ? Strings.WelcomecardNone(guildId) : summary.Subtitle,
                summary.Font,
                summary.TextColor,
                string.IsNullOrEmpty(summary.Avatar) ? Strings.WelcomecardNone(guildId) : summary.Avatar))
            .Build(), allowedMentions: AllowedMentions.None);
    }
}
