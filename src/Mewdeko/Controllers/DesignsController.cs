using System.IO;
using System.Net.Http;
using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Controllers.Common.Achievements;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.Designs;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Modules.CardDesigns.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Manages a community's card designs for the dashboard: the admin library, and what members own and equip.
/// </summary>
/// <remarks>
///     Uploads here count toward the community's card image cap shared with the achievement card designer. Member
///     uploads go through the member routes and a separate pool.
/// </remarks>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class DesignsController(
    CardDesignService designs,
    AchievementService achievements,
    AchievementIconService icons,
    IHttpClientFactory httpClientFactory,
    DiscordShardedClient client) : Controller
{
    private const int MaxDownloadBytes = 8 * 1024 * 1024;

    #region Library

    /// <summary>
    ///     Lists a community's designs.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="target">Only designs for this card: 0 rank, 1 achievement.</param>
    /// <returns>The designs with owner counts, linked shop items and background previews.</returns>
    [HttpGet]
    public async Task<IActionResult> List(ulong guildId, [FromQuery] int? target = null)
    {
        var infos = (await designs.GetInfosAsync(guildId))
            .Where(i => target is null || i.Design.Target == target)
            .ToList();
        return Ok(await MapAsync(guildId, infos));
    }

    /// <summary>
    ///     Gets one design.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <returns>The design, or 404.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(ulong guildId, int id)
    {
        var info = (await designs.GetInfosAsync(guildId)).FirstOrDefault(i => i.Design.Id == id);
        return info is null ? NotFound("No such design.") : Ok((await MapAsync(guildId, [info]))[0]);
    }

    /// <summary>
    ///     Creates a design. Members who already meet an unlock condition get it straight away.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="request">The design.</param>
    /// <returns>The created design, 400 for a bad request, or 409 when the name is taken.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] DesignRequest request)
    {
        if (!Enum.IsDefined((CardDesignTarget)request.Target))
            return BadRequest("Target must be 0 or 1.");
        if (await ValidateAsync(guildId, request) is { } problem)
            return BadRequest(problem);

        var (data, dataError) = await designs.NormalizeDataAsync(guildId, (CardDesignTarget)request.Target, request.Data);
        if (dataError is not null)
            return BadRequest(dataError);

        var draft = new CardDesign
        {
            Name = request.Name!,
            Target = (short)request.Target,
            Data = data!,
            Availability = (short)request.Availability,
            UnlockType = (short?)request.UnlockType,
            UnlockLevel = request.UnlockLevel,
            UnlockAchievementKey = request.UnlockAchievementKey?.Trim(),
            UnlockRoleId = request.UnlockRoleId
        };

        var (created, error) = await designs.CreateAsync(guildId, draft);
        return error == CardDesignError.NameTaken
            ? Conflict($"A design named '{request.Name!.Trim()}' already exists.")
            : await RespondAsync(guildId, created!.Id);
    }

    /// <summary>
    ///     Changes a design. Members who now meet an unlock condition get it straight away.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <param name="request">The new settings. The target can't change.</param>
    /// <returns>The updated design, 400 for a bad request, 404, or 409 when the name is taken.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(ulong guildId, int id, [FromBody] DesignRequest request)
    {
        var existing = await designs.GetDesignAsync(guildId, id);
        if (existing is null)
            return NotFound("No such design.");

        request.Name = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name;
        if (await ValidateAsync(guildId, request) is { } problem)
            return BadRequest(problem);

        string? data = null;
        if (request.Data is not null)
        {
            var (normalized, dataError) =
                await designs.NormalizeDataAsync(guildId, (CardDesignTarget)existing.Target, request.Data);
            if (dataError is not null)
                return BadRequest(dataError);
            data = normalized;
        }

        var (updated, error) = await designs.UpdateAsync(guildId, id, design =>
        {
            design.Name = request.Name!.Trim();
            if (data is not null)
                design.Data = data;
            design.Availability = (short)request.Availability;
            design.UnlockType = (short?)request.UnlockType;
            design.UnlockLevel = request.UnlockLevel;
            design.UnlockAchievementKey = request.UnlockAchievementKey?.Trim();
            design.UnlockRoleId = request.UnlockRoleId;
        });

        return error switch
        {
            CardDesignError.NameTaken => Conflict($"A design named '{request.Name!.Trim()}' already exists."),
            CardDesignError.NotFound => NotFound("No such design."),
            _ => await RespondAsync(guildId, updated!.Id)
        };
    }

    /// <summary>
    ///     Deletes a design. Ownership and equips go with it and a shop item selling it is turned off.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <returns>No content, or 404.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(ulong guildId, int id)
    {
        return await designs.DeleteAsync(guildId, id) ? NoContent() : NotFound("No such design.");
    }

    /// <summary>
    ///     Makes a design the community default for its card.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <returns>The updated design, or 404.</returns>
    [HttpPost("{id:int}/default")]
    public async Task<IActionResult> SetDefault(ulong guildId, int id)
    {
        return await designs.SetDefaultAsync(guildId, id, true) == CardDesignError.NotFound
            ? NotFound("No such design.")
            : await RespondAsync(guildId, id);
    }

    /// <summary>
    ///     Clears the community default of a design's card.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design, which must be the current default.</param>
    /// <returns>The updated design, or 404.</returns>
    [HttpDelete("{id:int}/default")]
    public async Task<IActionResult> ClearDefault(ulong guildId, int id)
    {
        return await designs.SetDefaultAsync(guildId, id, false) == CardDesignError.NotFound
            ? NotFound("No such design.")
            : await RespondAsync(guildId, id);
    }

    #endregion

    #region Background

    /// <summary>
    ///     Sets a design's background from an uploaded image, an image address, or a preset.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <param name="request">The image data, or a URL or preset reference.</param>
    /// <returns>The updated design, or why the image was refused.</returns>
    [HttpPost("{id:int}/background")]
    public async Task<IActionResult> SetBackground(ulong guildId, int id, [FromBody] DesignBackgroundRequest request)
    {
        var design = await designs.GetDesignAsync(guildId, id);
        if (design is null)
            return NotFound("No such design.");

        string reference;
        var url = request.Url?.Trim();
        if (!string.IsNullOrEmpty(request.Data))
        {
            if (DesignMapping.DecodeUpload(request.Data) is not { } bytes)
                return BadRequest(AchievementMapping.Describe(AchievementError.IconInvalid));
            var stored = await StoreAsync(guildId, bytes);
            if (stored.Error is not null)
                return BadRequest(stored.Error);
            reference = stored.Reference!;
        }
        else if (!string.IsNullOrEmpty(url) && CardPresets.TryParse(url, out _))
        {
            if (await designs.CheckReferenceAsync(guildId, url) is { } presetError)
                return BadRequest(presetError);
            reference = url;
        }
        else if (!string.IsNullOrEmpty(url))
        {
            var downloaded = await DownloadAsync(url);
            if (downloaded is null)
                return BadRequest("The image couldn't be downloaded, or it is over 8 MB.");
            var stored = await StoreAsync(guildId, downloaded);
            if (stored.Error is not null)
                return BadRequest(stored.Error);
            reference = stored.Reference!;
        }
        else
        {
            return BadRequest("Send image data or a URL.");
        }

        var data = CardDesignRules.WithBackground((CardDesignTarget)design.Target, design.Data, reference);
        var (updated, error) = await designs.UpdateAsync(guildId, id, d => d.Data = data);
        return error == CardDesignError.NotFound ? NotFound("No such design.") : await RespondAsync(guildId, updated!.Id);
    }

    /// <summary>
    ///     Removes a design's background image.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The design.</param>
    /// <returns>The updated design, or 404.</returns>
    [HttpDelete("{id:int}/background")]
    public async Task<IActionResult> ClearBackground(ulong guildId, int id)
    {
        var design = await designs.GetDesignAsync(guildId, id);
        if (design is null)
            return NotFound("No such design.");

        var data = CardDesignRules.WithBackground((CardDesignTarget)design.Target, design.Data, null);
        var (updated, error) = await designs.UpdateAsync(guildId, id, d => d.Data = data);
        return error == CardDesignError.NotFound ? NotFound("No such design.") : await RespondAsync(guildId, updated!.Id);
    }

    #endregion

    #region Member designs

    /// <summary>
    ///     Gets the rules for designs members make.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <returns>The rules with the member upload pool figures.</returns>
    [HttpGet("member-settings")]
    public async Task<IActionResult> GetMemberSettings(ulong guildId)
    {
        return Ok(await MapSettingsAsync(guildId, await designs.GetSettingsAsync(guildId)));
    }

    /// <summary>
    ///     Changes the rules for designs members make. Fields left out keep their value.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The rules, or 400 for a bad request.</returns>
    [HttpPut("member-settings")]
    public async Task<IActionResult> UpdateMemberSettings(ulong guildId, [FromBody] MemberDesignSettingsRequest request)
    {
        var current = await designs.GetSettingsAsync(guildId);
        var roles = request.RoleIds ?? CardDesignRules.ParseRoleIds(current.MemberRoleIds).ToList();
        var guild = client.GetGuild(guildId);
        if (guild is not null && roles.Any(r => guild.GetRole(r) is null))
            return BadRequest("One of those roles doesn't exist.");

        if (CardDesignRules.ValidateMemberSettings(request.Who ?? current.MemberWho, roles.Count,
                request.MinLevel ?? current.MemberMinLevel, request.Cost ?? current.MemberCost) is { } problem)
            return BadRequest(problem);

        var saved = await designs.SaveSettingsAsync(guildId, row =>
        {
            row.MemberEnabled = request.Enabled ?? row.MemberEnabled;
            row.MemberWho = (short)(request.Who ?? row.MemberWho);
            row.MemberRoleIds = CardDesignRules.FormatRoleIds(roles);
            row.MemberMinLevel = request.MinLevel ?? row.MemberMinLevel;
            row.MemberUploads = request.Uploads ?? row.MemberUploads;
            row.MemberReview = request.Review ?? row.MemberReview;
            row.MemberCost = request.Cost ?? row.MemberCost;
        });
        return Ok(await MapSettingsAsync(guildId, saved));
    }

    /// <summary>
    ///     Lists the designs members made.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="status">Only designs with this status: pending, approved or rejected.</param>
    /// <param name="target">Only designs for this card: 0 rank, 1 achievement.</param>
    /// <returns>The designs with the member upload pool figures.</returns>
    [HttpGet("member-designs")]
    public async Task<IActionResult> ListMemberDesigns(ulong guildId, [FromQuery] string? status = null,
        [FromQuery] int? target = null)
    {
        var all = await designs.GetMemberDesignsAsync(guildId);
        var items = all.Where(d => (target is null || d.Target == target) &&
                                   (string.IsNullOrEmpty(status) ||
                                    DesignMapping.StatusName(d).Equals(status, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(d => d.DateUpdated)
            .ToList();

        var guild = client.GetGuild(guildId);
        var response = new MemberDesignListResponse
        {
            UploadsUsed = await designs.CountMemberUploadsAsync(guildId),
            UploadsMax = AchievementIconService.MaxMemberCardUploads,
            Pending = all.Count(d => d.ReviewStatus == (short)CardDesignReview.Pending)
        };

        foreach (var design in items)
        {
            var owner = design.OwnerUserId!.Value;
            var user = guild?.GetUser(owner);
            response.Items.Add(new MemberDesignAdminResponse
            {
                Id = design.Id,
                UserId = owner,
                UserName = user?.DisplayName,
                AvatarUrl = user?.GetDisplayAvatarUrl(),
                Target = design.Target,
                Status = DesignMapping.StatusName(design),
                ReviewNote = design.ReviewNote,
                HasUpload = design.UploadId is not null,
                Active = await designs.IsMemberUsableAsync(design),
                Data = DesignMapping.ParseData(design.Data),
                BackgroundUrl = await designs.GetMemberPresetUrlAsync(design),
                DateAdded = design.DateAdded,
                DateUpdated = design.DateUpdated
            });
        }

        return Ok(response);
    }

    /// <summary>
    ///     Reads a member design's uploaded background, including one still waiting for review.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The member design.</param>
    /// <returns>The image as a data address, or 404.</returns>
    [HttpGet("member-designs/{id:int}/image")]
    public async Task<IActionResult> GetMemberDesignImage(ulong guildId, int id)
    {
        var design = await designs.GetMemberDesignByIdAsync(guildId, id);
        return design is null
            ? NotFound("No such design.")
            : Ok(new DesignImageResponse
            {
                DataUrl = await designs.GetMemberImageAsync(design)
            });
    }

    /// <summary>
    ///     Approves the uploaded background waiting on a member design.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The member design.</param>
    /// <returns>No content, 404, or 409 when nothing is waiting.</returns>
    [HttpPost("member-designs/{id:int}/approve")]
    public async Task<IActionResult> ApproveMemberDesign(ulong guildId, int id)
    {
        return (await designs.ApproveMemberAsync(guildId, id)).Outcome switch
        {
            MemberModerationOutcome.NotFound => NotFound("No such design."),
            MemberModerationOutcome.NothingPending => Conflict("That design has no upload waiting for review."),
            _ => NoContent()
        };
    }

    /// <summary>
    ///     Turns down the uploaded background waiting on a member design and tells the member.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The member design.</param>
    /// <param name="request">The reason, shown to the member.</param>
    /// <returns>Whether the member was told, 404, or 409 when nothing is waiting.</returns>
    [HttpPost("member-designs/{id:int}/reject")]
    public async Task<IActionResult> RejectMemberDesign(ulong guildId, int id,
        [FromBody] MemberDesignModerationRequest request)
    {
        var result = await designs.RejectMemberAsync(guildId, id, request.Reason);
        return result.Outcome switch
        {
            MemberModerationOutcome.NotFound => NotFound("No such design."),
            MemberModerationOutcome.NothingPending => Conflict("That design has no upload waiting for review."),
            _ => Ok(new MemberDesignModerationResponse
            {
                Notified = result.Notified
            })
        };
    }

    /// <summary>
    ///     Removes a member's design and tells them.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="id">The member design.</param>
    /// <param name="request">The reason, shown to the member.</param>
    /// <returns>Whether the member was told, or 404.</returns>
    [HttpPost("member-designs/{id:int}/remove")]
    public async Task<IActionResult> RemoveMemberDesign(ulong guildId, int id,
        [FromBody] MemberDesignModerationRequest request)
    {
        var result = await designs.RemoveMemberAsync(guildId, id, request.Reason);
        return result.Outcome == MemberModerationOutcome.NotFound
            ? NotFound("No such design.")
            : Ok(new MemberDesignModerationResponse
            {
                Notified = result.Notified
            });
    }

    #endregion

    #region Members

    /// <summary>
    ///     Lists the designs a member can use in this community: the ones they own and the free ones.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The member.</param>
    /// <returns>The member's designs per card, with equipped flags and the data needed for previews.</returns>
    [HttpGet("member/{userId}")]
    public async Task<IActionResult> GetMember(ulong guildId, ulong userId)
    {
        return Ok(await DesignMapping.BuildMemberAsync(designs, guildId, userId));
    }

    /// <summary>
    ///     Equips a design for a member, if they own it or it is free.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The member.</param>
    /// <param name="request">The design.</param>
    /// <returns>The member's designs after the change, 404, or 403 when they can't use that design.</returns>
    [HttpPost("member/{userId}/equip")]
    public async Task<IActionResult> Equip(ulong guildId, ulong userId, [FromBody] EquipDesignRequest request)
    {
        return await designs.EquipAsync(guildId, userId, request.DesignId) switch
        {
            EquipResult.NotFound => NotFound("No such design."),
            EquipResult.NotOwned => StatusCode(403, "You don't own that design."),
            _ => Ok(await DesignMapping.BuildMemberAsync(designs, guildId, userId))
        };
    }

    /// <summary>
    ///     Unequips a member's design for one card.
    /// </summary>
    /// <param name="guildId">The community.</param>
    /// <param name="userId">The member.</param>
    /// <param name="request">The card.</param>
    /// <returns>The member's designs after the change.</returns>
    [HttpPost("member/{userId}/unequip")]
    public async Task<IActionResult> Unequip(ulong guildId, ulong userId, [FromBody] UnequipDesignRequest request)
    {
        if (!Enum.IsDefined((CardDesignTarget)request.Target))
            return BadRequest("Target must be 0 or 1.");

        await designs.UnequipAsync(guildId, userId, (CardDesignTarget)request.Target);
        return Ok(await DesignMapping.BuildMemberAsync(designs, guildId, userId));
    }

    #endregion

    private async Task<string?> ValidateAsync(ulong guildId, DesignRequest request)
    {
        if (!Enum.IsDefined((CardDesignAvailability)request.Availability))
            return "Availability must be 0, 1 or 2.";

        var unlockType = request.UnlockType is { } type && Enum.IsDefined((CardDesignUnlockType)type)
            ? (CardDesignUnlockType?)type
            : null;
        if (request.Availability == (int)CardDesignAvailability.Unlock && unlockType is null)
            return "Unlock type must be 0, 1 or 2.";

        if (CardDesignRules.Validate(request.Name, (CardDesignAvailability)request.Availability, unlockType,
                request.UnlockLevel, request.UnlockAchievementKey, request.UnlockRoleId) is { } error)
            return error;
        if (request.Availability != (int)CardDesignAvailability.Unlock)
            return null;

        if (unlockType == CardDesignUnlockType.Achievement &&
            !(await achievements.GetCatalogAsync(guildId)).ByKey.ContainsKey(request.UnlockAchievementKey!.Trim()))
            return "That achievement doesn't exist.";
        if (unlockType == CardDesignUnlockType.Role &&
            client.GetGuild(guildId)?.GetRole(request.UnlockRoleId!.Value) is null)
            return "That role doesn't exist.";

        return null;
    }

    private async Task<MemberDesignSettingsResponse> MapSettingsAsync(ulong guildId, CardDesignSetting settings)
    {
        return new MemberDesignSettingsResponse
        {
            Enabled = settings.MemberEnabled,
            Who = settings.MemberWho,
            RoleIds = CardDesignRules.ParseRoleIds(settings.MemberRoleIds).ToList(),
            MinLevel = settings.MemberMinLevel,
            Uploads = settings.MemberUploads,
            Review = settings.MemberReview,
            Cost = settings.MemberCost,
            UploadsUsed = await designs.CountMemberUploadsAsync(guildId),
            UploadsMax = AchievementIconService.MaxMemberCardUploads,
            Pending = (await designs.GetMemberDesignsAsync(guildId))
                .Count(d => d.ReviewStatus == (short)CardDesignReview.Pending)
        };
    }

    private async Task<IActionResult> RespondAsync(ulong guildId, int id)
    {
        var info = (await designs.GetInfosAsync(guildId)).First(i => i.Design.Id == id);
        return Ok((await MapAsync(guildId, [info]))[0]);
    }

    private async Task<List<DesignResponse>> MapAsync(ulong guildId, IReadOnlyList<CardDesignInfo> infos)
    {
        var urls = await designs.ResolveBackgroundUrlsAsync(guildId, infos.Select(i => i.Design));
        var catalog = infos.Any(i => i.Design.UnlockAchievementKey is not null)
            ? await achievements.GetCatalogAsync(guildId)
            : null;
        var guild = client.GetGuild(guildId);

        return infos.Select(info =>
        {
            var key = info.Design.UnlockAchievementKey;
            var achievementName = key is not null && catalog?.ByKey.TryGetValue(key, out var definition) == true
                ? definition.Name
                : null;
            var roleName = info.Design.UnlockRoleId is { } roleId ? guild?.GetRole(roleId)?.Name : null;
            return DesignMapping.Map(info, urls.GetValueOrDefault(info.Design.Id), achievementName, roleName);
        }).ToList();
    }

    private async Task<(string? Reference, string? Error)> StoreAsync(ulong guildId, byte[] bytes)
    {
        var actor = await HttpContext.GetDashboardUserIdAsync() ?? 0;
        var result = await icons.UploadAsync(guildId, actor, bytes, AchievementIconService.CardKind);
        return result.Success
            ? (AchievementIcons.UploadPrefix + result.Value!.Id, null)
            : (null, AchievementMapping.Describe(result.Error));
    }

    private async Task<byte[]?> DownloadAsync(string url)
    {
        if (!CardDesignRules.IsValidBackgroundRef(url) || url.StartsWith("upload:", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxDownloadBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxDownloadBytes)
                    return null;
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or UriFormatException)
        {
            return null;
        }
    }

}
