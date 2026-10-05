using System.IO;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using Microsoft.Extensions.DependencyInjection;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Achievements.Services;

public sealed partial class AchievementService
{
    /// <summary>
    ///     Placeholders an unlock message can use, with what each becomes.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string Description)> UnlockPlaceholders =
    [
        ("%achievement.name%", "Name of the achievement"),
        ("%achievement.description%", "What it took"),
        ("%achievement.image%", "The unlock image, for an embed's image or thumbnail when unlock images are on"),
        ("%achievement.grade%", "Bronze, Silver, Gold, Emerald, Amethyst, or Champion"),
        ("%achievement.points%", "Points it gave"),
        ("%achievement.category%", "Its category"),
        ("%achievement.count%", "How many unlocked at once"),
        ("%achievement.list%", "Every achievement unlocked at once, one per line"),
        ("%achievements.points%", "The member's total points"),
        ("%achievements.unlocked%", "How many achievements the member has"),
        ("%achievements.tier%", "The member's rank")
    ];

    /// <summary>
    ///     Announces unlocks in the server and in DMs, following the server's and the member's choices.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="unlocked">What they unlocked.</param>
    /// <param name="channelId">Where it happened, or 0.</param>
    /// <returns>A task that completes when sent.</returns>
    private async Task AnnounceAsync(SocketGuildUser user, IReadOnlyList<AchievementDefinition> unlocked,
        ulong channelId)
    {
        if (unlocked.Count == 0)
            return;

        try
        {
            var guild = user.Guild;
            var settings = GetSettings(guild.Id);
            var mode = settings.AnnounceMode;
            if (mode == AchievementAnnounceMode.Silent)
                return;

            var prefs = await GetUserSettingsAsync(user.Id);
            var dmPref = (AchievementDmPreference)prefs.DmUnlocks;
            var sendDm = dmPref != AchievementDmPreference.Never &&
                         (mode == AchievementAnnounceMode.DmOnly || dmPref == AchievementDmPreference.Always ||
                          dmPref == AchievementDmPreference.ServerDefault && settings.Row.DmByDefault);

            ITextChannel? target = null;
            if (mode != AchievementAnnounceMode.DmOnly && prefs.ShowInLog)
            {
                var logChannel = settings.Row.LogChannelId is { } logId ? guild.GetTextChannel(logId) : null;
                var here = channelId != 0 ? guild.GetTextChannel(channelId) : null;
                if (here is not null && here.Id != logChannel?.Id && !CanAnnounceWhereItHappened(user, here, settings))
                    here = null;
                target = mode switch
                {
                    AchievementAnnounceMode.Here => here ?? logChannel,
                    AchievementAnnounceMode.LogChannel => logChannel,
                    _ => logChannel ?? here
                };

                if (target is not null)
                {
                    var perms = guild.CurrentUser.GetPermissions(target);
                    if (!perms.SendMessages || !perms.ViewChannel)
                        target = null;
                }
            }

            if (target is null && !sendDm)
                return;

            var summary = await GetSummaryAsync(guild.Id, user.Id);
            var catalog = await GetCatalogAsync(guild.Id);
            var mention = settings.Row.MentionUsers && prefs.MentionMe;

            if (target is not null)
                await SendAnnouncementAsync(target, user, unlocked, summary, catalog, settings, mention, target);

            if (sendDm)
            {
                try
                {
                    var dm = await user.CreateDMChannelAsync();
                    await SendAnnouncementAsync(dm, user, unlocked, summary, catalog, settings, false, null);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not DM achievement unlocks to {UserId}", user.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to announce achievements in {GuildId}", user.Guild.Id);
        }
    }

    /// <summary>
    ///     Whether an unlock may be announced in the channel it happened in. Quiet channels never take
    ///     announcements, and when the server asks for it, neither do channels the member cannot talk in.
    ///     A thread follows its parent channel.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="channel">Where it happened.</param>
    /// <param name="settings">The server's settings.</param>
    /// <returns>True when the announcement can go there.</returns>
    private static bool CanAnnounceWhereItHappened(SocketGuildUser user, SocketTextChannel channel,
        AchievementGuildSettings settings)
    {
        var thread = channel as SocketThreadChannel;
        var parent = thread?.ParentChannel;
        if (settings.QuietChannels.Contains(channel.Id) || parent is not null && settings.QuietChannels.Contains(parent.Id))
            return false;

        if (!settings.Row.RequireSendPermission)
            return true;

        if (thread is null)
        {
            var perms = user.GetPermissions(channel);
            return perms.ViewChannel && perms.SendMessages;
        }

        if (parent is null)
            return false;
        var parentPerms = user.GetPermissions(parent);
        return parentPerms.ViewChannel && parentPerms.SendMessagesInThreads;
    }

    private async Task SendAnnouncementAsync(IMessageChannel channel, SocketGuildUser user,
        IReadOnlyList<AchievementDefinition> unlocked, AchievementMemberSummary summary,
        AchievementGuildCatalog catalog, AchievementGuildSettings settings, bool mention, ITextChannel? context)
    {
        var guild = user.Guild;
        var allowed = mention
            ? new AllowedMentions
            {
                UserIds = [user.Id]
            }
            : AllowedMentions.None;

        await using var image = settings.Row.UnlockImage ? await RenderUnlockImageAsync(user, unlocked, catalog) : null;

        IUserMessage sent;
        if (!string.IsNullOrWhiteSpace(settings.Row.UnlockMessage))
        {
            var replacer = BuildReplacer(user, context, unlocked, summary, catalog);
            var processed = replacer.Replace(settings.Row.UnlockMessage) ?? "";
            sent = SmartEmbed.TryParse(processed, guild.Id, out var embeds, out var plainText, out var components)
                ? await SendAsync(channel, image, plainText, embeds, components?.Build(), allowed)
                : await SendAsync(channel, image, processed, null, null, allowed);
        }
        else
        {
            var embed = BuildUnlockEmbed(user, unlocked, summary, catalog, image is not null);
            sent = await SendAsync(channel, image, mention ? user.Mention : null, [embed], null, allowed);
        }

        if (settings.Row.DeleteAfter > 0 && channel is not IDMChannel)
            sent.DeleteAfter(settings.Row.DeleteAfter);
    }

    /// <summary>
    ///     The file name unlock images are attached under.
    /// </summary>
    public const string UnlockImageFileName = "achievement.png";

    /// <summary>
    ///     Longest a server can keep unlock messages before deleting them, in seconds.
    /// </summary>
    public const int MaxDeleteAfter = 86400;

    private static async Task<IUserMessage> SendAsync(IMessageChannel channel, Stream? image, string? text,
        Embed[]? embeds, MessageComponent? components, AllowedMentions allowed)
    {
        if (image is null)
            return await channel.SendMessageAsync(text, embeds: embeds, components: components, allowedMentions: allowed);

        return await channel.SendFileAsync(new FileAttachment(image, UnlockImageFileName), text, embeds: embeds,
            components: components, allowedMentions: allowed);
    }

    /// <summary>
    ///     Draws the unlock image for the best of several unlocks.
    /// </summary>
    /// <param name="user">Who unlocked them.</param>
    /// <param name="unlocked">What they unlocked.</param>
    /// <param name="catalog">The server's catalog.</param>
    /// <returns>The PNG, or null when drawing failed.</returns>
    public async Task<MemoryStream?> RenderUnlockImageAsync(SocketGuildUser user,
        IReadOnlyList<AchievementDefinition> unlocked, AchievementGuildCatalog catalog)
    {
        var best = unlocked.OrderByDescending(d => d.Grade).ThenByDescending(d => d.Points).First();
        return await RenderImageAsync(user, best, catalog, strings.AchievementImageUnlocked(user.Guild.Id),
            unlocked.Count > 1 ? strings.AchievementImageMore(user.Guild.Id, unlocked.Count - 1) : null, null, false);
    }

    /// <summary>
    ///     Draws the image of one achievement as a member sees it: unlocked, or locked with their progress.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="progress">Their progress on it.</param>
    /// <param name="catalog">The server's catalog.</param>
    /// <returns>The PNG, or null when drawing failed.</returns>
    public Task<MemoryStream?> RenderProgressImageAsync(SocketGuildUser user, AchievementProgress progress,
        AchievementGuildCatalog catalog)
    {
        var def = progress.Definition;
        var guildId = user.Guild.Id;
        if (progress.Unlocked)
            return RenderImageAsync(user, def, catalog, strings.AchievementImageUnlocked(guildId), null, null, false);

        var label = def.IsMetric && progress.Current is { } current
            ? strings.AchievementImageProgress(guildId, Math.Min(current, def.Threshold).ToString("N0"),
                def.Threshold.ToString("N0"))
            : null;
        return RenderImageAsync(user, def, catalog, strings.AchievementImageLocked(guildId), null,
            label is null ? null : ((float)progress.Fraction, label), true);
    }

    /// <summary>
    ///     Draws an achievement as just unlocked, for previewing an edit before it is saved.
    /// </summary>
    /// <param name="user">Who to draw it for.</param>
    /// <param name="definition">The achievement as edited.</param>
    /// <param name="catalog">The server's catalog.</param>
    /// <returns>The PNG, or null when drawing failed.</returns>
    public Task<MemoryStream?> RenderPreviewImageAsync(SocketGuildUser user, AchievementDefinition definition,
        AchievementGuildCatalog catalog)
    {
        return RenderImageAsync(user, definition, catalog, strings.AchievementImageUnlocked(user.Guild.Id), null, null,
            false);
    }

    /// <summary>
    ///     Draws a card design before it is saved, with every element it can show filled in: a sample "+2 more" badge
    ///     when unlocked, and sample progress when locked.
    /// </summary>
    /// <param name="user">Who to draw it for.</param>
    /// <param name="template">The design as edited.</param>
    /// <param name="definition">The achievement to show.</param>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="locked">Whether to draw the locked state.</param>
    /// <returns>The image and where each element landed, or null when drawing failed.</returns>
    public async Task<AchievementUnlockRenderer.RenderResult?> RenderCardPreviewAsync(SocketGuildUser user,
        AchievementCardTemplate template, AchievementDefinition definition, AchievementGuildCatalog catalog,
        bool locked)
    {
        var guildId = user.Guild.Id;
        var target = definition.IsMetric && definition.Threshold > 0 ? definition.Threshold : 100;
        var current = (long)Math.Round(target * 0.48);
        var data = locked
            ? await BuildImageDataAsync(user, definition, catalog, strings.AchievementImageLocked(guildId), null,
                (0.48f, strings.AchievementImageProgress(guildId, current.ToString("N0"), target.ToString("N0"))),
                true)
            : await BuildImageDataAsync(user, definition, catalog, strings.AchievementImageUnlocked(guildId),
                strings.AchievementImageMore(guildId, 2), null, false);
        try
        {
            return await services.GetRequiredService<AchievementUnlockRenderer>()
                .RenderWithLayoutAsync(data, template);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to draw an achievement card preview in {GuildId}", guildId);
            return null;
        }
    }

    private async Task<MemoryStream?> RenderImageAsync(SocketGuildUser user, AchievementDefinition def,
        AchievementGuildCatalog catalog, string label, string? more, (float Value, string Label)? progress, bool locked)
    {
        try
        {
            var data = await BuildImageDataAsync(user, def, catalog, label, more, progress, locked);
            return await services.GetRequiredService<AchievementUnlockRenderer>()
                .RenderAsync(data, await GetCardForAsync(user.Guild.Id, def));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to draw an achievement image in {GuildId}", user.Guild.Id);
            return null;
        }
    }

    private async Task<AchievementUnlockRenderer.UnlockImageData> BuildImageDataAsync(SocketGuildUser user,
        AchievementDefinition def, AchievementGuildCatalog catalog, string label, string? more,
        (float Value, string Label)? progress, bool locked)
    {
        var palette = await services.GetRequiredService<GuildPaletteService>().GetAsync(user.Guild);
        var category = catalog.Category(def.CategoryKey);
        return new AchievementUnlockRenderer.UnlockImageData
        {
            GuildId = user.Guild.Id,
            Palette = palette,
            Definition = def,
            Icon = catalog.IconFor(def),
            CategoryName = category.Name,
            CategoryIcon = AchievementIcons.Parse(category.Icon),
            Label = label,
            PointsLabel = strings.AchievementImagePoints(user.Guild.Id, def.Points.ToString("N0")),
            MemberName = user.DisplayName,
            AvatarUrl = user.GetDisplayAvatarUrl(size: 128),
            Scope = user.Guild.Name,
            MoreLabel = more,
            Progress = progress?.Value,
            ProgressLabel = progress?.Label,
            Locked = locked
        };
    }

    /// <summary>
    ///     The default unlock embed.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="unlocked">What they unlocked.</param>
    /// <param name="summary">Their totals after unlocking.</param>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="withImage">
    ///     Whether the unlock image is attached. It then carries a single unlock on its own, so the embed
    ///     only lists achievements when there are several.
    /// </param>
    /// <returns>The embed.</returns>
    public Embed BuildUnlockEmbed(SocketGuildUser user, IReadOnlyList<AchievementDefinition> unlocked,
        AchievementMemberSummary summary, AchievementGuildCatalog catalog, bool withImage = false)
    {
        var guildId = user.Guild.Id;
        var best = unlocked.OrderByDescending(d => d.Grade).ThenByDescending(d => d.Points).First();
        var title = unlocked.Count == 1
            ? strings.AchievementUnlockedTitle(guildId)
            : strings.AchievementUnlockedManyTitle(guildId, unlocked.Count);

        var lines = unlocked
            .OrderByDescending(d => d.Grade)
            .Take(10)
            .Select(d => strings.AchievementUnlockedLine(guildId, d.Name, AchievementCatalog.GetGrade(d.Grade).Name,
                d.Description, d.Points));
        var description = string.Join("\n", lines);
        if (unlocked.Count > 10)
            description += "\n" + strings.AchievementUnlockedMore(guildId, unlocked.Count - 10);

        var footer = strings.AchievementUnlockedFooter(guildId, summary.Points, summary.Tier.Name, summary.Unlocked,
            summary.Total);
        var color = AchievementCatalog.GetGrade(best.Grade).Color;

        var embed = new EmbedBuilder()
            .WithColor(new Color(color))
            .WithAuthor(user.DisplayName, user.GetDisplayAvatarUrl())
            .WithTitle(title)
            .WithFooter(footer);
        if (!withImage || unlocked.Count > 1)
            embed.WithDescription(description);
        if (withImage)
            embed.WithImageUrl($"attachment://{UnlockImageFileName}");
        return embed.Build();
    }

    private Replacer BuildReplacer(SocketGuildUser user, ITextChannel? channel,
        IReadOnlyList<AchievementDefinition> unlocked, AchievementMemberSummary summary,
        AchievementGuildCatalog catalog)
    {
        var best = unlocked.OrderByDescending(d => d.Grade).ThenByDescending(d => d.Points).First();
        var list = string.Join("\n", unlocked.Select(d => d.Name));
        return new ReplacementBuilder()
            .WithDefault(user, channel!, user.Guild, client)
            .WithOverride("%achievement.name%", () => best.Name)
            .WithOverride("%achievement.description%", () => best.Description)
            .WithOverride("%achievement.image%", () => $"attachment://{UnlockImageFileName}")
            .WithOverride("%achievement.grade%", () => AchievementCatalog.GetGrade(best.Grade).Name)
            .WithOverride("%achievement.points%", () => best.Points.ToString("N0"))
            .WithOverride("%achievement.category%", () => catalog.Category(best.CategoryKey).Name)
            .WithOverride("%achievement.count%", () => unlocked.Count.ToString())
            .WithOverride("%achievement.list%", () => list)
            .WithOverride("%achievements.points%", () => summary.Points.ToString("N0"))
            .WithOverride("%achievements.unlocked%", () => summary.Unlocked.ToString("N0"))
            .WithOverride("%achievements.tier%", () => summary.Tier.Name)
            .Build();
    }

    /// <summary>
    ///     Renders an unlock message for a preview, using sample achievements.
    /// </summary>
    /// <param name="user">Who to preview as.</param>
    /// <param name="source">The message source, or null for the default embed.</param>
    /// <returns>The plain text and embeds Discord would get.</returns>
    public async Task<(string? Text, Embed[]? Embeds)> PreviewUnlockAsync(SocketGuildUser user, string? source)
    {
        var catalog = await GetCatalogAsync(user.Guild.Id);
        var sample = catalog.Earnable.Where(d => !d.Hidden).Take(1).ToList();
        if (sample.Count == 0)
            sample = [AchievementCatalog.BuiltIns[0]];
        var summary = await GetSummaryAsync(user.Guild.Id, user.Id);

        if (string.IsNullOrWhiteSpace(source))
            return (null, [BuildUnlockEmbed(user, sample, summary, catalog)]);

        var processed = BuildReplacer(user, null, sample, summary, catalog).Replace(source) ?? "";
        return SmartEmbed.TryParse(processed, user.Guild.Id, out var embeds, out var plainText, out _)
            ? (plainText, embeds)
            : (processed, null);
    }
}
