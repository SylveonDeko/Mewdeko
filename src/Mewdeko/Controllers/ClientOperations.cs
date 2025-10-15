using System.Text.Json;
using System.Text.Json.Serialization;
using Mewdeko.Controllers.Common.ClientOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChannelType = Mewdeko.Controllers.Common.ClientOperations.ChannelType;

namespace Mewdeko.Controllers;

/// <summary>
///     Api endpoint for operations done via the discord client, such as getting roles, users, guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class ClientOperations(DiscordShardedClient client) : Controller
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    ///     Returns roles for a guild
    /// </summary>
    /// <param name="guildId">The guildid to check for roles</param>
    /// <returns>A 404 if the guildid doesnt exist in the bot, or a collection of roles</returns>
    [HttpGet("roles/{guildId}")]
    public async Task<IActionResult> GetRoles(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();


        return Ok(guild.Roles.Select(x => new NeededRoleInfo
        {
            Id = x.Id, Name = x.Name
        }));
    }

    /// <summary>
    ///     Gets category channels from a guild
    /// </summary>
    /// <param name="guildId">The guild id to get category channels from</param>
    /// <returns>Category channels or 404 if the guild is not found</returns>
    [HttpGet("categories/{guildId}")]
    public async Task<IActionResult> GetCategories(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var categories = guild.CategoryChannels.Select(x => new NeededRoleInfo
        {
            Id = x.Id, Name = x.Name
        });

        return Ok(categories);
    }

    /// <summary>
    ///     Gets channels of a specific type from a guildId
    /// </summary>
    /// <param name="guildId">The guild id to get channels from</param>
    /// <param name="channelType">A <see cref="ChannelType" /> for filtering</param>
    /// <returns>Channels based on the filter or 404 if the guild is not found</returns>
    [HttpGet("channels/{guildId}/{channelType}")]
    public async Task<IActionResult> GetChannels(ulong guildId, ChannelType channelType = ChannelType.None)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var channels = channelType switch
        {
            ChannelType.Text => guild.Channels
                .Where(x => x is ITextChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                }),
            ChannelType.Voice => guild.Channels
                .Where(x => x is IVoiceChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                }),
            ChannelType.Forum => guild.Channels
                .Where(x => x is IForumChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                }),
            ChannelType.Category => guild.Channels
                .Where(x => x is ICategoryChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                }),
            ChannelType.Announcement => guild.Channels
                .Where(x => x is INewsChannel)
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                }),
            _ => guild.Channels
                .Select(c => new NeededRoleInfo
                {
                    Id = c.Id, Name = c.Name
                })
        };

        return Ok(channels);
    }

    /// <summary>
    ///     Gets all IGuildUsers for a guild.
    /// </summary>
    /// <param name="guildId">The guildId to get the users for</param>
    /// <returns>404 if guild not found or the users if found.</returns>
    [HttpGet("users/{guildId}")]
    public async Task<IActionResult> GetUsers(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return NotFound();

        return Ok(JsonSerializer.Serialize(guild.Users.Select(x => new
        {
            UserId = x.Id, x.Username, AvatarUrl = x.GetAvatarUrl()
        }), Options));
    }

    /// <summary>
    ///     Gets all members in a guild
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <returns>The list of members in the guild</returns>
    [HttpGet("members/{guildId}")]
    public IActionResult GetGuildMembers(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var members = guild.Users.Select(user => new
        {
            Id = user.Id.ToString(),
            user.Username,
            DisplayName = user.Nickname ?? user.Username,
            AvatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
            user.IsBot
        });

        return Ok(members);
    }


    /// <summary>
    ///     Gets text channels from a guild
    /// </summary>
    /// <param name="guildId">The guild id to get text channels from</param>
    /// <returns>Text channels or 404 if the guild is not found</returns>
    [HttpGet("textchannels/{guildId}")]
    public async Task<IActionResult> GetTextChannels(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var channels = guild.TextChannels.Select(x => new
        {
            x.Id, x.Name
        });

        return Ok(channels);
    }

    /// <summary>
    ///     Gets a single user from a guild.
    /// </summary>
    /// <param name="guildId">The guildId to get the users for</param>
    /// <param name="userId">The user id of the user to get.</param>
    /// <returns>404 if guild not found or the users if found.</returns>
    [HttpGet("user/{guildId}/{userId}")]
    public async Task<IActionResult> GetUser(ulong guildId, ulong userId)
    {
        await Task.CompletedTask;
        var guild = client.Guilds.FirstOrDefault(x => x.Id == guildId);
        if (guild == null)
            return NotFound();

        var user = guild.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
            return NotFound();
        var partial = new
        {
            UserId = user.Id, user.Username, AvatarUrl = user.GetAvatarUrl()
        };
        return Ok(partial);
    }

    /// <summary>
    ///     Gets a list of guilds the bot and user have mutual
    /// </summary>
    /// <param name="userId">The user ID to check mutual guilds for</param>
    /// <param name="adminOnly">Whether to only return guilds where user has admin permissions (default: true)</param>
    /// <returns></returns>
    [HttpGet("mutualguilds/{userId}")]
    public async Task<IActionResult> GetMutualGuilds(ulong userId, [FromQuery] bool adminOnly = true)
    {
        await Task.CompletedTask;
        var guilds = client.Guilds;
        var mutuals = guilds
            .Where(x => x.Users.Any(y => y.Id == userId &&
                                         (adminOnly ? y.GuildPermissions.Has(GuildPermission.Administrator) : true)))
            .Select(g => new
            {
                id = g.Id,
                name = g.Name,
                icon = g.IconId,
                owner = g.OwnerId == userId,
                permissions = (int)g.GetUser(userId).GuildPermissions.RawValue,
                features = Enum.GetValues(typeof(GuildFeature)).Cast<GuildFeature>()
                    .Where(x => g.Features.Value.HasFlag(x)),
                banner = g.BannerUrl + "?size=4096",
                hasAdminAccess = g.GetUser(userId).GuildPermissions.Has(GuildPermission.Administrator)
            })
            .ToList();

        if (mutuals.Count != 0)
            return Ok(mutuals);
        return NotFound();
    }

    /// <summary>
    ///     Checks if this bot instance has the specified guild
    /// </summary>
    /// <param name="guildId">The guild ID to check</param>
    /// <returns>Whether this instance has the guild with basic info</returns>
    [HttpGet("hasguild/{guildId}")]
    public async Task<IActionResult> HasGuild(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);

        return Ok(new
        {
            hasGuild = guild != null,
            guildName = guild?.Name,
            memberCount = guild?.MemberCount,
            iconUrl = guild?.IconUrl,
            // Add more public info that might be useful for leaderboards
            createdAt = guild?.CreatedAt,
            description = guild?.Description,
            features = guild?.Features
        });
    }

    /// <summary>
    ///     Gets the guilds the bot is in
    /// </summary>
    /// <returns>A list of guildIds  the bot is in</returns>
    [HttpGet("guilds")]
    public async Task<IActionResult> GetGuilds()
    {
        await Task.CompletedTask;
        return Ok(JsonSerializer.Serialize(client.Guilds.Select(x => x.Id), Options));
    }

    /// <summary>
    ///     Gets forum channels with detailed information including tags and threads
    /// </summary>
    /// <param name="guildId">The guild ID to get forum channels from</param>
    /// <returns>Detailed forum channel information or 404 if guild not found</returns>
    [HttpGet("forumchannels/{guildId}")]
    public async Task<IActionResult> GetForumChannels(ulong guildId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound();

        var forumChannels = new List<ForumChannelInfo>();

        foreach (var forum in guild.Channels.OfType<IForumChannel>())
        {
            try
            {
                var activeThreads = await forum.GetActiveThreadsAsync();

                var forumInfo = new ForumChannelInfo
                {
                    Id = forum.Id,
                    Name = forum.Name,
                    Topic = forum.Topic,
                    RequiresTags = forum.Tags.Any(t => t.IsModerated),
                    MaxActiveThreads = null,
                    DefaultAutoArchiveDuration = (int)forum.DefaultAutoArchiveDuration,
                    Tags = forum.Tags.Select(tag => new ForumTagInfo
                    {
                        Id = tag.Id, Name = tag.Name, Emoji = tag.Emoji?.ToString(), IsModerated = tag.IsModerated
                    }).ToList(),
                    ActiveThreads = activeThreads.Select(thread => new ThreadInfo
                    {
                        Id = thread.Id,
                        Name = thread.Name,
                        AppliedTags = thread.AppliedTags?.ToList() ?? new List<ulong>(),
                        CreatorId = thread.OwnerId,
                        CreatedAt = thread.CreatedAt.UtcDateTime,
                        MessageCount = thread.MessageCount,
                        IsArchived = thread.IsArchived,
                        IsLocked = thread.IsLocked
                    }).ToList(),
                    TotalThreadCount = activeThreads.Count
                };

                forumChannels.Add(forumInfo);
            }
            catch (Exception)
            {
                // Continue with basic info if thread fetching fails
                forumChannels.Add(new ForumChannelInfo
                {
                    Id = forum.Id,
                    Name = forum.Name,
                    Topic = forum.Topic,
                    Tags = forum.Tags.Select(tag => new ForumTagInfo
                    {
                        Id = tag.Id, Name = tag.Name, Emoji = tag.Emoji?.ToString(), IsModerated = tag.IsModerated
                    }).ToList()
                });
            }
        }

        return Ok(forumChannels);
    }

    /// <summary>
    ///     Gets detailed information about a specific forum channel
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="forumId">The forum channel ID</param>
    /// <returns>Detailed forum information or 404 if not found</returns>
    [HttpGet("forumchannel/{guildId}/{forumId}")]
    public async Task<IActionResult> GetForumChannel(ulong guildId, ulong forumId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var forum = guild.GetChannel(forumId) as IForumChannel;
        if (forum == null)
            return NotFound("Forum channel not found");

        try
        {
            var activeThreads = await forum.GetActiveThreadsAsync();

            var forumInfo = new ForumChannelInfo
            {
                Id = forum.Id,
                Name = forum.Name,
                Topic = forum.Topic,
                RequiresTags = forum.Tags.Any(t => t.IsModerated),
                MaxActiveThreads = null,
                DefaultAutoArchiveDuration = (int)forum.DefaultAutoArchiveDuration,
                Tags = forum.Tags.Select(tag => new ForumTagInfo
                {
                    Id = tag.Id, Name = tag.Name, Emoji = tag.Emoji?.ToString(), IsModerated = tag.IsModerated
                }).ToList(),
                ActiveThreads = activeThreads.Select(thread => new ThreadInfo
                {
                    Id = thread.Id,
                    Name = thread.Name,
                    AppliedTags = thread.AppliedTags?.ToList() ?? new List<ulong>(),
                    CreatorId = thread.OwnerId,
                    CreatedAt = thread.CreatedAt.UtcDateTime,
                    MessageCount = thread.MessageCount,
                    IsArchived = thread.IsArchived,
                    IsLocked = thread.IsLocked
                }).ToList(),
                TotalThreadCount = activeThreads.Count
            };

            return Ok(forumInfo);
        }
        catch (Exception ex)
        {
            // Return basic forum info if thread fetching fails
            var basicInfo = new ForumChannelInfo
            {
                Id = forum.Id,
                Name = forum.Name,
                Topic = forum.Topic,
                Tags = forum.Tags.Select(tag => new ForumTagInfo
                {
                    Id = tag.Id, Name = tag.Name, Emoji = tag.Emoji?.ToString(), IsModerated = tag.IsModerated
                }).ToList()
            };

            return Ok(basicInfo);
        }
    }

    /// <summary>
    ///     Gets threads for a specific forum channel
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="forumId">The forum channel ID</param>
    /// <param name="includeArchived">Whether to include archived threads</param>
    /// <returns>List of threads or 404 if not found</returns>
    [HttpGet("forumthreads/{guildId}/{forumId}")]
    public async Task<IActionResult> GetForumThreads(ulong guildId, ulong forumId,
        [FromQuery] bool includeArchived = false)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var forum = guild.GetChannel(forumId) as IForumChannel;
        if (forum == null)
            return NotFound("Forum channel not found");

        try
        {
            var activeThreads = await forum.GetActiveThreadsAsync();
            var threads = activeThreads.Select(thread => new ThreadInfo
            {
                Id = thread.Id,
                Name = thread.Name,
                AppliedTags = thread.AppliedTags?.ToList() ?? new List<ulong>(),
                CreatorId = thread.OwnerId,
                CreatedAt = thread.CreatedAt.UtcDateTime,
                MessageCount = thread.MessageCount,
                IsArchived = thread.IsArchived,
                IsLocked = thread.IsLocked
            });

            if (includeArchived)
            {
                var archivedThreads = await forum.GetPublicArchivedThreadsAsync();
                var allThreads = threads.Concat(archivedThreads.Select(thread => new ThreadInfo
                {
                    Id = thread.Id,
                    Name = thread.Name,
                    AppliedTags = thread.AppliedTags?.ToList() ?? new List<ulong>(),
                    CreatorId = thread.OwnerId,
                    CreatedAt = thread.CreatedAt.UtcDateTime,
                    MessageCount = thread.MessageCount,
                    IsArchived = thread.IsArchived,
                    IsLocked = thread.IsLocked
                }));

                return Ok(allThreads);
            }

            return Ok(threads);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to retrieve forum threads: {ex.Message}");
        }
    }

    /// <summary>
    ///     Gets emojis from mutual guilds for the emoji picker
    /// </summary>
    /// <param name="userId">The user ID to get mutual guild emojis for</param>
    /// <param name="adminOnly">Whether to only include guilds where user has admin permissions (default: true)</param>
    /// <returns>List of guild emojis grouped by guild</returns>
    [HttpGet("emojis/{userId}")]
    public async Task<IActionResult> GetMutualGuildEmojis(ulong userId, [FromQuery] bool adminOnly = false)
    {
        await Task.CompletedTask;
        var guilds = client.Guilds;
        var mutualGuilds = guilds
            .Where(x => x.Users.Any(y => y.Id == userId &&
                                         (!adminOnly || y.GuildPermissions.Has(GuildPermission.Administrator))))
            .ToList();

        if (mutualGuilds.Count == 0)
            return NotFound("No mutual guilds found");

        var result = new List<GuildEmojiInfo>();

        foreach (var guild in mutualGuilds)
        {
            var emojis = guild.Emotes
                .Where(e => e.IsAvailable != false) // Filter out unavailable emojis
                .Select(e => new EmojiInfo
                {
                    Id = e.Id,
                    Name = e.Name,
                    Animated = e.Animated,
                    IsAvailable = e.IsAvailable,
                    RoleIds = e.RoleIds?.ToList() ?? new List<ulong>(),
                    RequireColons = e.RequireColons,
                    Url = e.Url
                })
                .ToList();

            // Only include guilds that have emojis
            if (emojis.Count > 0)
            {
                result.Add(new GuildEmojiInfo
                {
                    Guild = new GuildInfo
                    {
                        Id = guild.Id, Name = guild.Name, IconUrl = guild.IconUrl
                    },
                    Emojis = emojis
                });
            }
        }

        return Ok(result);
    }
}