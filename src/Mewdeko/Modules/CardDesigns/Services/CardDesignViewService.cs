using System.Text;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.CardDesigns.Services;

/// <summary>
///     Builds the replies of the design commands.
/// </summary>
/// <param name="designs">The card design library.</param>
/// <param name="strings">The bot strings.</param>
public sealed class CardDesignViewService(CardDesignService designs, GeneratedBotStrings strings) : INService
{
    private const int MaxListLength = 3800;

    /// <summary>
    ///     Equips a design by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="name">The design name.</param>
    /// <returns>Whether it worked, and what to tell the member.</returns>
    public async Task<(bool Ok, string Message)> EquipAsync(ulong guildId, ulong userId, string name)
    {
        var design = CardDesignRules.FindByName(await designs.GetDesignsAsync(guildId), name);
        if (design is null)
            return (false, strings.DesignNotFound(guildId, name.Trim()));

        return await designs.EquipAsync(guildId, userId, design.Id) switch
        {
            EquipResult.Equipped => (true,
                strings.DesignEquipped(guildId, design.Name, TargetName(guildId, (CardDesignTarget)design.Target))),
            EquipResult.NotOwned => (false, strings.DesignNotOwned(guildId, design.Name)),
            _ => (false, strings.DesignNotFound(guildId, name.Trim()))
        };
    }

    /// <summary>
    ///     Unequips the design for one card.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="card">The card as typed: rank or achievement.</param>
    /// <returns>Whether it worked, and what to tell the member.</returns>
    public async Task<(bool Ok, string Message)> UnequipAsync(ulong guildId, ulong userId, string card)
    {
        if (ParseCard(card) is not { } chosen)
            return (false, strings.DesignUnknownCard(guildId));

        var name = TargetName(guildId, chosen);
        return await designs.UnequipAsync(guildId, userId, chosen)
            ? (true, strings.DesignUnequipped(guildId, name))
            : (false, strings.DesignNothingEquipped(guildId, name));
    }

    /// <summary>
    ///     Lists the designs a member can use.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The title and text of the list, or null when there is nothing to list.</returns>
    public async Task<(string Title, string Text)?> ListAsync(ulong guildId, ulong userId)
    {
        var available = await designs.GetAvailableAsync(guildId, userId);
        if (available.Count == 0)
            return null;

        var equipped = (await designs.GetEquippedIdsAsync(guildId, userId)).Values.ToHashSet();
        var text = new StringBuilder();
        var shown = 0;
        foreach (var (design, source) in available.OrderBy(a => a.Design.Target).ThenBy(a => a.Design.Name))
        {
            var target = TargetName(guildId, (CardDesignTarget)design.Target);
            var line = equipped.Contains(design.Id)
                ? strings.DesignListEntryEquipped(guildId, design.Name, target, SourceName(guildId, source))
                : strings.DesignListEntry(guildId, design.Name, target, SourceName(guildId, source));
            if (text.Length + line.Length > MaxListLength)
            {
                text.AppendLine(strings.DesignListMore(guildId, available.Count - shown));
                break;
            }

            text.AppendLine(line);
            shown++;
        }

        return (strings.DesignListTitle(guildId), text.ToString());
    }

    /// <summary>
    ///     Lists the member designs waiting for a moderator.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The title and text of the list, or null when nothing is waiting.</returns>
    public async Task<(string Title, string Text)?> PendingAsync(ulong guildId)
    {
        var pending = (await designs.GetMemberDesignsAsync(guildId))
            .Where(d => d.ReviewStatus == (short)CardDesignReview.Pending)
            .ToList();
        if (pending.Count == 0)
            return null;

        var text = new StringBuilder();
        var shown = 0;
        foreach (var design in pending)
        {
            var line = strings.MemberDesignListEntry(guildId, design.OwnerUserId!.Value,
                TargetName(guildId, (CardDesignTarget)design.Target));
            if (text.Length + line.Length > MaxListLength)
            {
                text.AppendLine(strings.DesignListMore(guildId, pending.Count - shown));
                break;
            }

            text.AppendLine(line);
            shown++;
        }

        return (strings.MemberDesignListTitle(guildId), text.ToString());
    }

    /// <summary>
    ///     Approves a member's waiting upload.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="card">The card as typed.</param>
    /// <returns>Whether it worked, and what to tell the moderator.</returns>
    public async Task<(bool Ok, string Message)> ApproveAsync(ulong guildId, ulong userId, string card)
    {
        if (ParseCard(card) is not { } target)
            return (false, strings.DesignUnknownCard(guildId));

        var design = await designs.GetMemberDesignAsync(guildId, userId, target);
        var name = TargetName(guildId, target);
        if (design is null)
            return (false, strings.MemberDesignNotFound(guildId, userId, name));

        return (await designs.ApproveMemberAsync(guildId, design.Id)).Outcome switch
        {
            MemberModerationOutcome.Done => (true, strings.MemberDesignApproved(guildId, name, userId)),
            _ => (false, strings.MemberDesignNothingPending(guildId))
        };
    }

    /// <summary>
    ///     Turns a member's waiting upload down and tells them.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="card">The card as typed.</param>
    /// <param name="reason">Why, or null.</param>
    /// <returns>Whether it worked, and what to tell the moderator.</returns>
    public async Task<(bool Ok, string Message)> RejectAsync(ulong guildId, ulong userId, string card, string? reason)
    {
        if (ParseCard(card) is not { } target)
            return (false, strings.DesignUnknownCard(guildId));

        var design = await designs.GetMemberDesignAsync(guildId, userId, target);
        var name = TargetName(guildId, target);
        if (design is null)
            return (false, strings.MemberDesignNotFound(guildId, userId, name));

        var result = await designs.RejectMemberAsync(guildId, design.Id, reason);
        if (result.Outcome != MemberModerationOutcome.Done)
            return (false, strings.MemberDesignNothingPending(guildId));

        var message = strings.MemberDesignRejected(guildId, name, userId);
        return (true, result.Notified ? message : $"{message} {strings.MemberDesignNotNotified(guildId)}");
    }

    /// <summary>
    ///     Describes a member's own designs and whether they can make one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The title and text.</returns>
    public async Task<(string Title, string Text)> MyDesignsAsync(ulong guildId, ulong userId)
    {
        var settings = await designs.GetSettingsAsync(guildId);
        var lines = new List<string>();
        switch (await designs.GetEligibilityAsync(guildId, userId))
        {
            case MemberEligibility.Disabled:
                lines.Add(strings.MemberDesignMyDisabled(guildId));
                return (strings.MemberDesignMyTitle(guildId), lines[0]);
            case MemberEligibility.NeedsRole:
                lines.Add(strings.MemberDesignMyNeedsRole(guildId,
                    string.Join(", ", CardDesignRules.ParseRoleIds(settings.MemberRoleIds).Select(r => $"<@&{r}>"))));
                break;
            case MemberEligibility.NeedsLevel:
                lines.Add(strings.MemberDesignMyNeedsLevel(guildId, settings.MemberMinLevel));
                break;
        }

        foreach (var target in new[] { CardDesignTarget.Rank, CardDesignTarget.Achievement })
        {
            var name = TargetName(guildId, target);
            var design = await designs.GetMemberDesignAsync(guildId, userId, target);
            lines.Add(design is null
                ? strings.MemberDesignMyNone(guildId, name)
                : strings.MemberDesignMyEntry(guildId, name, StatusName(guildId, (CardDesignReview)design.ReviewStatus)));
        }

        return (strings.MemberDesignMyTitle(guildId), string.Join("\n", lines));
    }

    private static CardDesignTarget? ParseCard(string card)
    {
        return card.Trim().ToLowerInvariant() switch
        {
            "rank" or "xp" or "level" => CardDesignTarget.Rank,
            "achievement" or "achievements" or "ach" => CardDesignTarget.Achievement,
            _ => null
        };
    }

    private string StatusName(ulong guildId, CardDesignReview status)
    {
        return status switch
        {
            CardDesignReview.Pending => strings.MemberDesignStatusPending(guildId),
            CardDesignReview.Rejected => strings.MemberDesignStatusRejected(guildId),
            _ => strings.MemberDesignStatusApproved(guildId)
        };
    }

    private string TargetName(ulong guildId, CardDesignTarget target)
    {
        return target == CardDesignTarget.Rank
            ? strings.DesignTargetRank(guildId)
            : strings.DesignTargetAchievement(guildId);
    }

    private string SourceName(ulong guildId, CardDesignSource? source)
    {
        return source switch
        {
            CardDesignSource.Purchase => strings.DesignSourcePurchase(guildId),
            CardDesignSource.Level => strings.DesignSourceLevel(guildId),
            CardDesignSource.Achievement => strings.DesignSourceAchievement(guildId),
            CardDesignSource.Role => strings.DesignSourceRole(guildId),
            _ => strings.DesignSourceFree(guildId)
        };
    }
}
