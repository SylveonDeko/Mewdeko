using DataModel;
using LinqToDB;
using Mewdeko.Controllers.Common.Filter;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing message filtering (words, invites, links)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class FilterController(FilterService filterService, IDataConnectionFactory dbFactory) : Controller
{
    /// <summary>
    ///     Gets all filter settings for a guild
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetFilterSettings(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(gc => gc.GuildId == guildId);
        var filteredWords = await filterService.FilteredWordsForServer(guildId) ?? new HashSet<string>();

        var autoBanWords = await db.AutoBanWords
            .Where(abw => abw.GuildId == guildId)
            .Select(abw => abw.Word)
            .ToListAsync();

        var wordFilterChannels = await db.FilterWordsChannelIds
            .Where(fc => fc.GuildId == guildId)
            .Select(fc => fc.ChannelId)
            .ToListAsync();

        var inviteFilterChannels = await db.FilterInvitesChannelIds
            .Where(fc => fc.GuildId == guildId)
            .Select(fc => fc.ChannelId)
            .ToListAsync();

        var linkFilterChannels = await db.FilterLinksChannelIds
            .Where(fc => fc.GuildId == guildId)
            .Select(fc => fc.ChannelId)
            .ToListAsync();

        return Ok(new
        {
            serverSettings = new
            {
                filterWords = guildConfig?.FilterWords ?? false,
                filterInvites = guildConfig?.FilterInvites ?? false,
                filterLinks = guildConfig?.FilterLinks ?? false,
                warnOnFilteredWord = await filterService.GetFw(guildId) == 1,
                warnOnInvite = await filterService.GetInvWarn(guildId) == 1
            },
            filteredWords = filteredWords.ToList(),
            autoBanWords,
            channelSettings = new
            {
                wordFilterChannels, inviteFilterChannels, linkFilterChannels
            }
        });
    }

    /// <summary>
    ///     Updates server-wide filter settings
    /// </summary>
    [HttpPut("server-settings")]
    public async Task<IActionResult> UpdateServerFilterSettings(ulong guildId,
        [FromBody] ServerFilterSettingsRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildConfig = await db.GuildConfigs.FirstOrDefaultAsync(gc => gc.GuildId == guildId);
        if (guildConfig == null)
        {
            guildConfig = new GuildConfig
            {
                GuildId = guildId
            };
            await db.InsertAsync(guildConfig);
        }

        guildConfig.FilterWords = request.FilterWords;
        guildConfig.FilterInvites = request.FilterInvites;
        guildConfig.FilterLinks = request.FilterLinks;

        await db.UpdateAsync(guildConfig);

        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Manages filtered words
    /// </summary>
    [HttpPost("words/{word}")]
    public async Task<IActionResult> ToggleFilteredWord(ulong guildId, string word)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.FilteredWords
            .FirstOrDefaultAsync(fw => fw.GuildId == guildId && fw.Word == word);

        if (existing != null)
        {
            await db.DeleteAsync(existing);
            return Ok(new
            {
                added = false, word
            });
        }
        else
        {
            await db.InsertAsync(new FilteredWord
            {
                GuildId = guildId, Word = word
            });
            return Ok(new
            {
                added = true, word
            });
        }
    }

    /// <summary>
    ///     Gets filtered words
    /// </summary>
    [HttpGet("words")]
    public async Task<IActionResult> GetFilteredWords(ulong guildId)
    {
        var words = await filterService.FilteredWordsForServer(guildId) ?? new HashSet<string>();
        return Ok(new
        {
            words = words.ToList()
        });
    }

    /// <summary>
    ///     Clears all filtered words
    /// </summary>
    [HttpDelete("words")]
    public async Task<IActionResult> ClearFilteredWords(ulong guildId)
    {
        await filterService.ClearFilteredWords(guildId);
        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Manages auto-ban words
    /// </summary>
    [HttpPost("autoban-words/{word}")]
    public async Task<IActionResult> ToggleAutoBanWord(ulong guildId, string word)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.AutoBanWords
            .FirstOrDefaultAsync(abw => abw.GuildId == guildId && abw.Word == word);

        if (existing != null)
        {
            await filterService.UnBlacklist(word, guildId);
            return Ok(new
            {
                added = false, word
            });
        }
        else
        {
            await filterService.WordBlacklist(word, guildId);
            return Ok(new
            {
                added = true, word
            });
        }
    }

    /// <summary>
    ///     Gets auto-ban words
    /// </summary>
    [HttpGet("autoban-words")]
    public async Task<IActionResult> GetAutoBanWords(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var words = await db.AutoBanWords
            .Where(abw => abw.GuildId == guildId)
            .Select(abw => abw.Word)
            .ToListAsync();

        return Ok(new
        {
            words
        });
    }

    /// <summary>
    ///     Updates warning settings
    /// </summary>
    [HttpPut("warnings")]
    public async Task<IActionResult> UpdateWarningSettings(ulong guildId, [FromBody] WarningSettingsRequest request)
    {
        // Note: These methods require an IGuild object, so we'll need to fetch it
        var client = HttpContext.RequestServices.GetRequiredService<DiscordShardedClient>();
        var guild = client.GetGuild(guildId);

        if (guild == null)
            return NotFound("Guild not found");

        if (request.WarnOnFilteredWord != null)
            await filterService.SetFwarn(guild, request.WarnOnFilteredWord.Value ? "y" : "n");

        if (request.WarnOnInvite != null)
            await filterService.InvWarn(guild, request.WarnOnInvite.Value ? "y" : "n");

        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Toggles word filtering for a channel
    /// </summary>
    [HttpPost("channels/{channelId}/word-filter")]
    public async Task<IActionResult> ToggleChannelWordFilter(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.FilterWordsChannelIds
            .FirstOrDefaultAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (existing != null)
        {
            await db.DeleteAsync(existing);
            return Ok(new
            {
                enabled = false, channelId
            });
        }
        else
        {
            await db.InsertAsync(new FilterWordsChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });
            return Ok(new
            {
                enabled = true, channelId
            });
        }
    }

    /// <summary>
    ///     Toggles invite filtering for a channel
    /// </summary>
    [HttpPost("channels/{channelId}/invite-filter")]
    public async Task<IActionResult> ToggleChannelInviteFilter(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.FilterInvitesChannelIds
            .FirstOrDefaultAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (existing != null)
        {
            await db.DeleteAsync(existing);
            return Ok(new
            {
                enabled = false, channelId
            });
        }
        else
        {
            await db.InsertAsync(new FilterInvitesChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });
            return Ok(new
            {
                enabled = true, channelId
            });
        }
    }

    /// <summary>
    ///     Toggles link filtering for a channel
    /// </summary>
    [HttpPost("channels/{channelId}/link-filter")]
    public async Task<IActionResult> ToggleChannelLinkFilter(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.FilterLinksChannelIds
            .FirstOrDefaultAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (existing != null)
        {
            await db.DeleteAsync(existing);
            return Ok(new
            {
                enabled = false, channelId
            });
        }
        else
        {
            await db.InsertAsync(new FilterLinksChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });
            return Ok(new
            {
                enabled = true, channelId
            });
        }
    }
}