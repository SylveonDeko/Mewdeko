using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.Birthday.Services;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Modules.RoleMenus.Services;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Services;

/// <summary>
///     Writes MEE6 settings into the matching Mewdeko features, recording what each section replaced or created so
///     the whole import can be undone.
/// </summary>
public class Mee6SettingsImporter(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    GuildSettingsService guildSettings,
    AutoAssignRoleService autoAssignRoles,
    XpService xpService,
    XpRewardManager xpRewards,
    XpCacheManager xpCache,
    BirthdayService birthdays,
    ChatTriggersService chatTriggers,
    RoleCommandsService reactionRoles,
    RoleMenuService roleMenus,
    ProtectionService protection,
    StreamNotificationService streams,
    ShopService shop,
    ILogger<Mee6SettingsImporter> logger) : INService
{
    /// <summary>
    ///     Describes what each section of a MEE6 export would write on this server.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="plan">The MEE6 settings.</param>
    public List<ImportSettingsSection> Describe(ulong guildId, Mee6SettingsPlan plan)
    {
        var guild = client.GetGuild(guildId);
        string Channel(ulong? id) => id is { } c && guild?.GetTextChannel(c) is { } ch ? $"#{ch.Name}" : "a deleted channel";
        string Role(ulong id) => guild?.GetRole(id) is { } role ? $"@{role.Name}" : "a deleted role";
        var sections = new List<ImportSettingsSection>();

        if (plan.Welcome is { } welcome)
        {
            var details = new List<string>();
            if (welcome.ChannelId is not null) details.Add($"Welcome message in {Channel(welcome.ChannelId)}");
            if (welcome.DmMessage is not null) details.Add("Private welcome message");
            if (welcome.ByeChannelId is not null) details.Add($"Goodbye message in {Channel(welcome.ByeChannelId)}");
            if (welcome.JoinRoles.Count > 0)
                details.Add($"Join roles: {string.Join(", ", welcome.JoinRoles.Select(Role))}");
            sections.Add(new ImportSettingsSection { Key = Mee6Section.Welcome, Count = details.Count, Details = details });
        }

        if (plan.Levels is { } levels)
        {
            var details = new List<string>
            {
                levels.AnnouncementType switch
                {
                    0 => "Level-up announcements off",
                    2 => "Level-up announcements in DMs",
                    3 => $"Level-up announcements in {Channel(levels.AnnouncementChannelId)}",
                    _ => "Level-up announcements where the member spoke"
                },
                $"XP rate x{levels.XpRate:0.##}"
            };
            if (levels.RoleRewards.Count > 0)
                details.Add($"{levels.RoleRewards.Count} level roles" +
                            (levels.RemovePreviousRewards ? ", replacing the previous one" : ""));
            if (levels.ExcludedChannels.Count > 0)
                details.Add($"{levels.ExcludedChannels.Count} channels that earn no XP");
            if (levels.ExcludedRoles.Count > 0)
                details.Add($"{levels.ExcludedRoles.Count} roles that earn no XP");
            sections.Add(new ImportSettingsSection { Key = Mee6Section.Levels, Count = details.Count, Details = details });
        }

        if (plan.Birthdays is { } birthday)
        {
            var details = new List<string>();
            if (birthday.ChannelId is not null) details.Add($"Wishes in {Channel(birthday.ChannelId)}");
            if (birthday.RoleId is { } role) details.Add($"Birthday role {Role(role)}");
            if (!string.IsNullOrWhiteSpace(birthday.Message)) details.Add("Birthday message");
            sections.Add(new ImportSettingsSection { Key = Mee6Section.Birthdays, Count = details.Count, Details = details });
        }

        if (plan.Commands.Count > 0)
        {
            sections.Add(new ImportSettingsSection
            {
                Key = Mee6Section.Commands,
                Count = plan.Commands.Count,
                Details = plan.Commands.Select(x => $"!{x.Name}" + (x.Disabled ? " (off)" : "")).ToList()
            });
        }

        if (plan.RoleMessages.Count > 0)
        {
            sections.Add(new ImportSettingsSection
            {
                Key = Mee6Section.ReactionRoles,
                Count = plan.RoleMessages.Count,
                Details = plan.RoleMessages.Select(x => x.IsReaction
                        ? $"{x.Name} in {Channel(x.ChannelId)}: {x.Options.Count} reactions, kept on the same message"
                        : $"{x.Name} in {Channel(x.ChannelId)}: {x.Options.Count} buttons, posted again as a Mewdeko menu")
                    .ToList()
            });
        }

        if (plan.AutoMod is { } mod)
        {
            var details = new List<string>();
            if (mod.BadWords.Count > 0)
                details.Add($"{mod.BadWords.Count} filtered words" + (mod.WarnOnWords ? ", with a warning" : ""));
            if (mod.FilterInvites) details.Add("Invite filter" + (mod.WarnOnInvites ? ", with a warning" : ""));
            if (mod.FilterLinks) details.Add("Link filter");
            if (mod.SpamThreshold > 0) details.Add($"Spam limit of {mod.SpamThreshold} messages");
            if (mod.MentionThreshold > 0) details.Add($"Mention limit of {mod.MentionThreshold} per message");
            details.AddRange(mod.Punishments.Select(x =>
                $"{x.Action} at {x.Warnings} warnings" + (x.Minutes > 0 ? $" for {x.Minutes} minutes" : "")));
            sections.Add(new ImportSettingsSection { Key = Mee6Section.AutoMod, Count = details.Count, Details = details });
        }

        if (plan.Streams.Count > 0)
        {
            sections.Add(new ImportSettingsSection
            {
                Key = Mee6Section.Twitch,
                Count = plan.Streams.Count,
                Details = plan.Streams.Select(x => $"{x.Login} in {Channel(x.ChannelId)}").ToList()
            });
        }

        if (plan.Economy is { } economy)
        {
            var details = new List<string>();
            if (economy.CurrencyEmojiId is not null)
                details.Add($"Currency emoji{(economy.CurrencyName is null ? "" : $" for {economy.CurrencyName}")}");
            details.AddRange(economy.Items.Select(x => $"Shop item {x.Name} for {x.Price}"));
            sections.Add(new ImportSettingsSection { Key = Mee6Section.Economy, Count = details.Count, Details = details });
        }

        return sections.Where(x => x.Count > 0).ToList();
    }

    /// <summary>
    ///     Applies the chosen sections of a MEE6 export.
    /// </summary>
    /// <param name="guildId">The server to write to.</param>
    /// <param name="plan">The MEE6 settings.</param>
    /// <param name="sections">The sections to apply, from <see cref="Mee6Section" />.</param>
    /// <returns>What was written per section, and the undo record as JSON.</returns>
    public async Task<(List<Mee6SectionResult> Results, string Undo)> ApplyAsync(ulong guildId, Mee6SettingsPlan plan,
        IReadOnlyCollection<string> sections)
    {
        var guild = client.GetGuild(guildId) ?? throw new ImportException(ImportError.NotFound);
        var emojis = guild.Emotes
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, x => x.First().ToString());
        var channels = guild.TextChannels
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, x => x.First().Id);
        string Resolve(string? text) => Mee6Text.ResolveMentions(text ?? "", emojis, channels);

        var undo = new Mee6Undo();
        var results = new List<Mee6SectionResult>();

        foreach (var section in Mee6Section.All.Where(sections.Contains))
        {
            try
            {
                var result = section switch
                {
                    Mee6Section.Welcome when plan.Welcome is { } p => await ApplyWelcomeAsync(guild, p, undo, Resolve),
                    Mee6Section.Levels when plan.Levels is { } p => await ApplyLevelsAsync(guild, p, undo, Resolve),
                    Mee6Section.Birthdays when plan.Birthdays is { } p => await ApplyBirthdaysAsync(guild, p, undo,
                        Resolve),
                    Mee6Section.Commands => await ApplyCommandsAsync(guild, plan.Commands, undo, Resolve),
                    Mee6Section.ReactionRoles => await ApplyRoleMessagesAsync(guild, plan.RoleMessages, undo, Resolve),
                    Mee6Section.AutoMod when plan.AutoMod is { } p => await ApplyAutoModAsync(guild, p, undo),
                    Mee6Section.Twitch => await ApplyStreamsAsync(guild, plan.Streams, undo, Resolve),
                    Mee6Section.Economy when plan.Economy is { } p => await ApplyEconomyAsync(guild, p, undo),
                    _ => null
                };
                if (result is not null)
                    results.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MEE6 {Section} import failed for guild {GuildId}", section, guildId);
                results.Add(new Mee6SectionResult(section, 0, 0, true));
            }
        }

        guildSettings.ClearCacheForGuild(guildId);
        return (results, JsonSerializer.Serialize(undo));
    }

    /// <summary>
    ///     Puts back what a MEE6 settings import changed.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="undoJson">The undo record saved by <see cref="ApplyAsync" />.</param>
    public async Task UndoAsync(ulong guildId, string undoJson)
    {
        var undo = JsonSerializer.Deserialize<Mee6Undo>(undoJson) ?? new Mee6Undo();
        await using var db = await dbFactory.CreateConnectionAsync();

        if (undo.MultiGreetIds.Count > 0)
            await db.MultiGreets.Where(x => x.GuildId == guildId && undo.MultiGreetIds.Contains(x.Id)).DeleteAsync();

        if (undo.Greet is { } greet || undo.Filter is not null || undo.CurrencyEmoji is not null)
        {
            var config = await guildSettings.GetGuildConfig(guildId, true);
            if (config is not null)
            {
                if (undo.Greet is { } g)
                {
                    config.SendDmGreetMessage = g.SendDm;
                    config.DmGreetMessageText = g.DmText;
                    config.ByeMessageChannelId = g.ByeChannel;
                    config.SendChannelByeMessage = g.SendBye;
                    config.ChannelByeMessageText = g.ByeText;
                    config.AutoAssignRoleId = g.AutoAssignRoles;
                }

                if (undo.Filter is { } f)
                {
                    config.FilterWords = f.Words;
                    config.FilterInvites = f.Invites;
                    config.FilterLinks = f.Links;
                    config.Fwarn = f.WordWarn;
                    config.Invwarn = f.InviteWarn;
                }

                if (undo.CurrencyEmoji is { } emoji)
                    config.CurrencyEmoji = emoji.Value;

                await guildSettings.UpdateGuildConfig(guildId, config);
            }
        }

        if (undo.Xp is { } xp)
        {
            await xpService.UpdateGuildXpSettingsAsync(guildId, s =>
            {
                s.LevelUpChannel = xp.LevelUpChannel;
                s.XpMultiplier = xp.Multiplier;
                s.ExclusiveRoleRewards = xp.ExclusiveRewards;
            });
        }

        if (undo.LevelUpMessageIds.Count > 0)
            await db.XpLevelUpMessages.Where(x => x.GuildId == guildId && undo.LevelUpMessageIds.Contains(x.Id))
                .DeleteAsync();

        foreach (var exclusion in undo.AddedExclusions)
            await xpService.IncludeItemAsync(guildId, exclusion.Id, (ExcludedItemType)exclusion.Type);

        foreach (var reward in undo.Rewards)
            await xpRewards.SetRoleRewardAsync(guildId, reward.Level, reward.RoleId);

        if (undo.Birthday is { } birthday)
        {
            await birthdays.UpdateBirthdayConfigAsync(guildId, c =>
            {
                c.BirthdayChannelId = birthday.Channel;
                c.BirthdayRoleId = birthday.Role;
                c.BirthdayMessage = birthday.Message;
                c.EnabledFeatures = birthday.Features;
            });
        }

        foreach (var id in undo.TriggerIds)
            await chatTriggers.DeleteAsync(guildId, id);

        foreach (var id in undo.ReactionRoleMessageIds)
            await reactionRoles.RemoveByIdAsync(guildId, id);

        foreach (var id in undo.RoleMenuIds)
            await roleMenus.DeleteAsync(guildId, id);

        if (undo.FilteredWordIds.Count > 0)
            await db.FilteredWords.Where(x => x.GuildId == guildId && undo.FilteredWordIds.Contains(x.Id)).DeleteAsync();

        if (undo.AntiSpam is { } spam)
        {
            if (spam.Existed)
                await protection.StartAntiSpamAsync(guildId, spam.Threshold, (PunishmentAction)spam.Action,
                    spam.Minutes, spam.RoleId);
            else
                await protection.TryStopAntiSpam(guildId);
        }

        if (undo.AntiMassMention is { } mention)
        {
            if (mention.Existed)
                await protection.StartAntiMassMentionAsync(guildId, mention.Threshold, mention.Window,
                    mention.MaxInWindow, mention.IgnoreBots, (PunishmentAction)mention.Action, mention.Minutes,
                    mention.RoleId);
            else
                await protection.TryStopAntiMassMention(guildId);
        }

        if (undo.PunishmentCounts.Count > 0)
        {
            await db.WarningPunishments.Where(x => x.GuildId == guildId && undo.PunishmentCounts.Contains(x.Count))
                .DeleteAsync();
            foreach (var previous in undo.PreviousPunishments)
            {
                await db.InsertAsync(new WarningPunishment
                {
                    GuildId = guildId,
                    Count = previous.Count,
                    Punishment = previous.Action,
                    Time = previous.Time,
                    RoleId = previous.RoleId,
                    DateAdded = DateTime.UtcNow
                });
            }
        }

        if (undo.StreamIds.Count > 0)
        {
            var followed = await db.FollowedStreams.Where(x => x.GuildId == guildId).OrderBy(x => x.Id)
                .Select(x => x.Id).ToListAsync();
            foreach (var index in undo.StreamIds.Select(id => followed.IndexOf(id)).Where(x => x >= 0)
                         .OrderByDescending(x => x))
                await streams.UnfollowStreamAsync(guildId, index);
        }

        foreach (var name in undo.ShopItemNames)
            await shop.RemoveItemAsync(guildId, name);

        guildSettings.ClearCacheForGuild(guildId);
        await xpCache.ClearGuildXpCachesAsync(guildId);
    }

    private async Task<Mee6SectionResult> ApplyWelcomeAsync(SocketGuild guild, Mee6WelcomePlan plan, Mee6Undo undo,
        Func<string?, string> resolve)
    {
        var written = 0;
        var skipped = 0;
        await using var db = await dbFactory.CreateConnectionAsync();

        if (plan.ChannelId is { } channelId && guild.GetTextChannel(channelId) is not null &&
            !string.IsNullOrWhiteSpace(plan.ChannelMessage))
        {
            var id = await db.InsertWithInt32IdentityAsync(new MultiGreet
            {
                GuildId = guild.Id,
                ChannelId = channelId,
                Message = resolve(plan.ChannelMessage),
                DateAdded = DateTime.UtcNow
            });
            undo.MultiGreetIds.Add(id);
            written++;
        }
        else if (plan.ChannelId is not null)
        {
            skipped++;
        }

        var config = await guildSettings.GetGuildConfig(guild.Id, true);
        if (config is not null)
        {
            undo.Greet = new Mee6Undo.GreetState(config.SendDmGreetMessage, config.DmGreetMessageText,
                config.ByeMessageChannelId, config.SendChannelByeMessage, config.ChannelByeMessageText,
                config.AutoAssignRoleId);

            if (!string.IsNullOrWhiteSpace(plan.DmMessage))
            {
                config.SendDmGreetMessage = true;
                config.DmGreetMessageText = resolve(plan.DmMessage);
                written++;
            }

            if (plan.ByeChannelId is { } byeChannel && guild.GetTextChannel(byeChannel) is not null)
            {
                config.ByeMessageChannelId = byeChannel;
                config.SendChannelByeMessage = true;
                config.ChannelByeMessageText = resolve(plan.ByeMessage);
                written++;
            }

            await guildSettings.UpdateGuildConfig(guild.Id, config);
        }

        var roles = plan.JoinRoles.Where(x => guild.GetRole(x) is not null).ToList();
        skipped += plan.JoinRoles.Count - roles.Count;
        if (roles.Count > 0)
        {
            var current = (await autoAssignRoles.TryGetNormalRoles(guild.Id)).ToList();
            await autoAssignRoles.SetAarRolesAsync(guild.Id, current.Union(roles));
            written += roles.Count;
        }

        return new Mee6SectionResult(Mee6Section.Welcome, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyLevelsAsync(SocketGuild guild, Mee6LevelsPlan plan, Mee6Undo undo,
        Func<string?, string> resolve)
    {
        var written = 0;
        var skipped = 0;
        var settings = await xpService.GetGuildXpSettingsAsync(guild.Id);
        undo.Xp = new Mee6Undo.XpState(settings.LevelUpChannel, settings.XpMultiplier, settings.ExclusiveRoleRewards);

        await xpService.UpdateGuildXpSettingsAsync(guild.Id, s =>
        {
            if (plan.AnnouncementType == 3 && plan.AnnouncementChannelId is { } channel &&
                guild.GetTextChannel(channel) is not null)
                s.LevelUpChannel = channel;
            s.XpMultiplier = plan.XpRate;
            s.ExclusiveRoleRewards = plan.RemovePreviousRewards;
        });
        written++;

        if (plan.AnnouncementType != 0 && !string.IsNullOrWhiteSpace(plan.Message))
        {
            await xpService.AddLevelUpMessageAsync(guild.Id, resolve(plan.Message));
            await using var db = await dbFactory.CreateConnectionAsync();
            var id = await db.XpLevelUpMessages.Where(x => x.GuildId == guild.Id).OrderByDescending(x => x.Id)
                .Select(x => x.Id).FirstOrDefaultAsync();
            if (id > 0)
                undo.LevelUpMessageIds.Add(id);
            written++;
        }

        foreach (var (ids, type) in new[]
                 {
                     (plan.ExcludedChannels.Where(x => guild.GetChannel(x) is not null).ToList(),
                         ExcludedItemType.Channel),
                     (plan.ExcludedRoles.Where(x => guild.GetRole(x) is not null).ToList(), ExcludedItemType.Role)
                 })
        {
            var existing = (await xpService.GetExcludedItemsAsync(guild.Id, type)).ToHashSet();
            foreach (var id in ids.Where(x => !existing.Contains(x)))
            {
                await xpService.ExcludeItemAsync(guild.Id, id, type);
                undo.AddedExclusions.Add(new Mee6Undo.Exclusion(id, (int)type));
                written++;
            }
        }

        var currentRewards = (await xpService.GetRoleRewardsAsync(guild.Id)).ToDictionary(x => x.Level);
        foreach (var reward in plan.RoleRewards)
        {
            if (guild.GetRole(reward.RoleId) is null)
            {
                skipped++;
                continue;
            }

            currentRewards.TryGetValue(reward.Level, out var before);
            undo.Rewards.Add(new Mee6Undo.Reward(reward.Level, before?.RoleId));
            await xpRewards.SetRoleRewardAsync(guild.Id, reward.Level, reward.RoleId);
            written++;
        }

        await xpCache.ClearGuildXpCachesAsync(guild.Id);
        return new Mee6SectionResult(Mee6Section.Levels, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyBirthdaysAsync(SocketGuild guild, Mee6BirthdayPlan plan, Mee6Undo undo,
        Func<string?, string> resolve)
    {
        var current = await birthdays.GetBirthdayConfigAsync(guild.Id);
        undo.Birthday = new Mee6Undo.BirthdayState(current.BirthdayChannelId, current.BirthdayRoleId,
            current.BirthdayMessage, current.EnabledFeatures);

        var channel = plan.ChannelId is { } c && guild.GetTextChannel(c) is not null ? c : (ulong?)null;
        var role = plan.RoleId is { } r && guild.GetRole(r) is not null ? r : (ulong?)null;
        await birthdays.UpdateBirthdayConfigAsync(guild.Id, config =>
        {
            if (channel is not null)
            {
                config.BirthdayChannelId = channel;
                config.EnabledFeatures |= (int)BirthdayFeature.Announcements;
            }

            if (role is not null)
            {
                config.BirthdayRoleId = role;
                config.EnabledFeatures |= (int)BirthdayFeature.BirthdayRole;
            }

            if (!string.IsNullOrWhiteSpace(plan.Message))
                config.BirthdayMessage = resolve(plan.Message);
        });

        var written = (channel is null ? 0 : 1) + (role is null ? 0 : 1) +
                      (string.IsNullOrWhiteSpace(plan.Message) ? 0 : 1);
        var skipped = (plan.ChannelId is not null && channel is null ? 1 : 0) +
                      (plan.RoleId is not null && role is null ? 1 : 0);
        return new Mee6SectionResult(Mee6Section.Birthdays, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyCommandsAsync(SocketGuild guild, List<Mee6CommandPlan> commands,
        Mee6Undo undo, Func<string?, string> resolve)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = (await db.ChatTriggers.Where(x => x.GuildId == guild.Id).Select(x => x.Trigger).ToListAsync())
            .Where(x => x is not null).Select(x => x!.ToLowerInvariant()).ToHashSet();

        var written = 0;
        var skipped = 0;
        foreach (var command in commands)
        {
            if (existing.Contains(command.Name))
            {
                skipped++;
                continue;
            }

            var roles = command.GrantedRoles.Where(x => guild.GetRole(x) is not null).ToList();
            var response = resolve(command.Response);
            var more = command.MoreResponses.Select(resolve).ToList();
            var trigger = await chatTriggers.AddTrigger(guild.Id, new ChatTrigger
            {
                GuildId = guild.Id,
                Trigger = command.Name,
                Response = response.Length > 0 ? response : null,
                NoRespond = response.Length == 0,
                PrefixType = (int)RequirePrefixType.Custom,
                CustomPrefix = "!",
                DmResponse = command.Private,
                GrantedRoles = roles.Count > 0 ? string.Join("@@@", roles) : null,
                AdditionalResponses = more.Count > 0 ? string.Join("@@@", more) : null,
                ResponseMode = (int)(more.Count > 0 ? CtResponseMode.Random : CtResponseMode.Single),
                ValidTriggerTypes = (int)ChatTriggerType.Message,
                AllowTarget = response.Contains("%target", StringComparison.OrdinalIgnoreCase),
                IsDisabled = command.Disabled,
                Category = "MEE6",
                DateAdded = DateTime.UtcNow
            });
            undo.TriggerIds.Add(trigger.Id);
            existing.Add(command.Name);
            written++;
        }

        return new Mee6SectionResult(Mee6Section.Commands, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyRoleMessagesAsync(SocketGuild guild,
        List<Mee6RoleMessagePlan> messages, Mee6Undo undo, Func<string?, string> resolve)
    {
        var (_, current) = await reactionRoles.Get(guild.Id);
        var known = current?.Select(x => x.MessageId).ToHashSet() ?? [];
        var index = current?.Count ?? 0;
        var written = 0;
        var skipped = 0;

        string? EmojiText(Mee6RoleOption option)
        {
            if (option.EmojiId is { } id)
                return guild.Emotes.FirstOrDefault(x => x.Id == id)?.ToString();
            return string.IsNullOrWhiteSpace(option.EmojiName) ? null : option.EmojiName;
        }

        foreach (var message in messages)
        {
            if (guild.GetTextChannel(message.ChannelId) is null)
            {
                skipped++;
                continue;
            }

            if (message.IsReaction)
            {
                if (message.MessageId == 0 || known.Contains(message.MessageId))
                {
                    skipped++;
                    continue;
                }

                var pairs = message.Options
                    .Select(x => (Emoji: EmojiText(x), Role: x.Roles.FirstOrDefault(r => guild.GetRole(r) is not null)))
                    .Where(x => x.Emoji is not null && x.Role != 0)
                    .ToList();
                if (pairs.Count == 0)
                {
                    skipped++;
                    continue;
                }

                var record = new ReactionRoleMessage
                {
                    ChannelId = message.ChannelId,
                    MessageId = message.MessageId,
                    Exclusive = message.Single,
                    Index = index++,
                    DateAdded = DateTime.UtcNow,
                    ReactionRoles = pairs.Select(x => new ReactionRole
                    {
                        EmoteName = x.Emoji, RoleId = x.Role, DateAdded = DateTime.UtcNow
                    }).ToList()
                };
                if (await reactionRoles.Add(guild.Id, record))
                {
                    undo.ReactionRoleMessageIds.Add(record.Id);
                    known.Add(message.MessageId);
                    written++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            var options = message.Options
                .Select(x => (Option: x, Role: x.Roles.FirstOrDefault(r => guild.GetRole(r) is not null)))
                .Where(x => x.Role != 0)
                .Take(25)
                .Select(x => new RoleMenuOptionDraft
                {
                    RoleId = x.Role,
                    Label = x.Option.Label,
                    Emoji = EmojiText(x.Option),
                    ButtonStyle = x.Option.ButtonStyle
                })
                .ToList();
            if (options.Count == 0)
            {
                skipped++;
                continue;
            }

            var created = await roleMenus.CreateAsync(guild.Id, null, new RoleMenuDraft
            {
                Name = message.Name,
                ChannelId = message.ChannelId,
                Message = string.IsNullOrWhiteSpace(message.Message) ? null : resolve(message.Message),
                Style = RoleMenuStyle.Buttons,
                Mode = message.Single ? RoleMenuMode.Exclusive : RoleMenuMode.Multi,
                Options = options
            });
            if (created is { Success: true, Menu: { } menu })
            {
                undo.RoleMenuIds.Add(menu.Id);
                written++;
            }
            else
            {
                skipped++;
            }
        }

        return new Mee6SectionResult(Mee6Section.ReactionRoles, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyAutoModAsync(SocketGuild guild, Mee6AutoModPlan plan, Mee6Undo undo)
    {
        var written = 0;
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await guildSettings.GetGuildConfig(guild.Id, true);
        if (config is not null)
        {
            undo.Filter = new Mee6Undo.FilterState(config.FilterWords, config.FilterInvites, config.FilterLinks,
                config.Fwarn, config.Invwarn);
            if (plan.BadWords.Count > 0)
            {
                config.FilterWords = true;
                if (plan.WarnOnWords)
                    config.Fwarn = 1;
            }

            if (plan.FilterInvites)
            {
                config.FilterInvites = true;
                if (plan.WarnOnInvites)
                    config.Invwarn = 1;
            }

            if (plan.FilterLinks)
                config.FilterLinks = true;
            await guildSettings.UpdateGuildConfig(guild.Id, config);
            written++;
        }

        var existingWords = (await db.FilteredWords.Where(x => x.GuildId == guild.Id).Select(x => x.Word)
            .ToListAsync()).ToHashSet();
        foreach (var word in plan.BadWords.Where(x => !existingWords.Contains(x)))
        {
            var id = await db.InsertWithInt32IdentityAsync(new FilteredWord
            {
                GuildId = guild.Id, Word = word, DateAdded = DateTime.UtcNow
            });
            undo.FilteredWordIds.Add(id);
            written++;
        }

        if (plan.SpamThreshold > 0)
        {
            var before = await db.AntiSpamSettings.FirstOrDefaultAsync(x => x.GuildId == guild.Id);
            undo.AntiSpam = new Mee6Undo.ProtectionState(before is not null, before?.MessageThreshold ?? 0,
                before?.Action ?? 0, before?.MuteTime ?? 0, before?.RoleId, 0, 0, false);
            await protection.StartAntiSpamAsync(guild.Id, Math.Max(2, plan.SpamThreshold), PunishmentAction.Timeout,
                5, null);
            written++;
        }

        if (plan.MentionThreshold > 0)
        {
            var before = await db.AntiMassMentionSettings.FirstOrDefaultAsync(x => x.GuildId == guild.Id);
            undo.AntiMassMention = new Mee6Undo.ProtectionState(before is not null, before?.MentionThreshold ?? 0,
                before?.Action ?? 0, before?.MuteTime ?? 0, before?.RoleId, before?.TimeWindowSeconds ?? 0,
                before?.MaxMentionsInTimeWindow ?? 0, before?.IgnoreBots ?? false);
            await protection.StartAntiMassMentionAsync(guild.Id, plan.MentionThreshold, 60,
                plan.MentionThreshold * 3, true, PunishmentAction.Timeout, 5, null);
            written++;
        }

        if (plan.Punishments.Count > 0)
        {
            var counts = plan.Punishments.Select(x => x.Warnings).ToList();
            undo.PreviousPunishments = await db.WarningPunishments
                .Where(x => x.GuildId == guild.Id && counts.Contains(x.Count))
                .Select(x => new Mee6Undo.Punishment(x.Count, x.Punishment, x.Time, x.RoleId))
                .ToListAsync();
            undo.PunishmentCounts = counts;
            await db.WarningPunishments.Where(x => x.GuildId == guild.Id && counts.Contains(x.Count)).DeleteAsync();

            foreach (var punishment in plan.Punishments)
            {
                var (action, minutes) = punishment.Action switch
                {
                    "kick" => (PunishmentAction.Kick, 0),
                    "ban" => (PunishmentAction.Ban, 0),
                    "tempban" or "temp_ban" => (PunishmentAction.Ban, punishment.Minutes),
                    _ => (PunishmentAction.Timeout, Math.Clamp(punishment.Minutes, 1, 40320))
                };
                await db.InsertAsync(new WarningPunishment
                {
                    GuildId = guild.Id,
                    Count = punishment.Warnings,
                    Punishment = (int)action,
                    Time = minutes,
                    DateAdded = DateTime.UtcNow
                });
                written++;
            }
        }

        return new Mee6SectionResult(Mee6Section.AutoMod, written, 0);
    }

    private async Task<Mee6SectionResult> ApplyStreamsAsync(SocketGuild guild, List<Mee6StreamPlan> plans,
        Mee6Undo undo, Func<string?, string> resolve)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = (await db.FollowedStreams.Where(x => x.GuildId == guild.Id).Select(x => x.Username)
            .ToListAsync()).Where(x => x is not null).Select(x => x!.ToLowerInvariant()).ToHashSet();

        var written = 0;
        var skipped = 0;
        foreach (var plan in plans)
        {
            if (existing.Contains(plan.Login) || guild.GetTextChannel(plan.ChannelId) is null)
            {
                skipped++;
                continue;
            }

            var data = await streams.FollowStream(guild.Id, plan.ChannelId, $"https://twitch.tv/{plan.Login}");
            if (data is null)
            {
                skipped++;
                continue;
            }

            var followed = await db.FollowedStreams.Where(x => x.GuildId == guild.Id).OrderBy(x => x.Id).ToListAsync();
            var index = followed.FindLastIndex(x => string.Equals(x.Username, data.UniqueName,
                StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                undo.StreamIds.Add(followed[index].Id);
                if (!string.IsNullOrWhiteSpace(plan.Message))
                    await streams.SetStreamMessage(guild.Id, index, resolve(plan.Message));
            }

            existing.Add(plan.Login);
            written++;
        }

        return new Mee6SectionResult(Mee6Section.Twitch, written, skipped);
    }

    private async Task<Mee6SectionResult> ApplyEconomyAsync(SocketGuild guild, Mee6EconomyPlan plan, Mee6Undo undo)
    {
        var written = 0;
        var skipped = 0;

        if (plan.CurrencyEmojiId is { } emojiId && guild.Emotes.FirstOrDefault(x => x.Id == emojiId) is { } emote)
        {
            var config = await guildSettings.GetGuildConfig(guild.Id, true);
            if (config is not null)
            {
                undo.CurrencyEmoji = new Mee6Undo.TextValue(config.CurrencyEmoji);
                config.CurrencyEmoji = emote.ToString();
                await guildSettings.UpdateGuildConfig(guild.Id, config);
                written++;
            }
        }

        var existing = (await shop.GetItemsAsync(guild.Id, true)).Select(x => x.Name.ToLowerInvariant()).ToHashSet();
        foreach (var item in plan.Items)
        {
            if (existing.Contains(item.Name.ToLowerInvariant()))
            {
                skipped++;
                continue;
            }

            var role = item.RoleId is { } r && guild.GetRole(r) is not null ? r : (ulong?)null;
            var added = await shop.AddItemAsync(new ShopItem
            {
                GuildId = guild.Id,
                Name = item.Name,
                Description = item.Description,
                Price = Math.Max(0, item.Price),
                ItemType = (int)(role is null ? Database.Enums.ShopItemType.Collectible : Database.Enums.ShopItemType.Role),
                RoleId = role,
                Stock = -1,
                Enabled = true,
                DateAdded = DateTime.UtcNow
            });
            if (added is null)
            {
                skipped++;
                continue;
            }

            undo.ShopItemNames.Add(item.Name);
            existing.Add(item.Name.ToLowerInvariant());
            written++;
        }

        return new Mee6SectionResult(Mee6Section.Economy, written, skipped);
    }
}

/// <summary>
///     What one section of a MEE6 import wrote.
/// </summary>
/// <param name="Section">The section key.</param>
/// <param name="Written">Settings or items written.</param>
/// <param name="Skipped">Items left out, such as deleted channels or roles and names already in use.</param>
/// <param name="Failed">Whether the section stopped with an error.</param>
public sealed record Mee6SectionResult(string Section, int Written, int Skipped, bool Failed = false);

/// <summary>
///     Everything a MEE6 settings import changed, kept so it can be put back.
/// </summary>
public sealed class Mee6Undo
{
    /// <summary>Greetings the import created.</summary>
    public List<int> MultiGreetIds { get; set; } = [];

    /// <summary>Greeting settings before the import.</summary>
    public GreetState? Greet { get; set; }

    /// <summary>XP settings before the import.</summary>
    public XpState? Xp { get; set; }

    /// <summary>Level-up messages the import created.</summary>
    public List<int> LevelUpMessageIds { get; set; } = [];

    /// <summary>Channels and roles the import stopped from earning XP.</summary>
    public List<Exclusion> AddedExclusions { get; set; } = [];

    /// <summary>Level roles before the import.</summary>
    public List<Reward> Rewards { get; set; } = [];

    /// <summary>Birthday settings before the import.</summary>
    public BirthdayState? Birthday { get; set; }

    /// <summary>Chat triggers the import created.</summary>
    public List<int> TriggerIds { get; set; } = [];

    /// <summary>Reaction role messages the import created.</summary>
    public List<int> ReactionRoleMessageIds { get; set; } = [];

    /// <summary>Role menus the import posted.</summary>
    public List<int> RoleMenuIds { get; set; } = [];

    /// <summary>Filter switches before the import.</summary>
    public FilterState? Filter { get; set; }

    /// <summary>Filtered words the import added.</summary>
    public List<int> FilteredWordIds { get; set; } = [];

    /// <summary>Anti-spam settings before the import.</summary>
    public ProtectionState? AntiSpam { get; set; }

    /// <summary>Anti-mention settings before the import.</summary>
    public ProtectionState? AntiMassMention { get; set; }

    /// <summary>Warning counts the import set punishments for.</summary>
    public List<int> PunishmentCounts { get; set; } = [];

    /// <summary>Punishments at those counts before the import.</summary>
    public List<Punishment> PreviousPunishments { get; set; } = [];

    /// <summary>Followed streams the import added.</summary>
    public List<int> StreamIds { get; set; } = [];

    /// <summary>The currency emoji before the import, when the import changed it.</summary>
    public TextValue? CurrencyEmoji { get; set; }

    /// <summary>Shop items the import added.</summary>
    public List<string> ShopItemNames { get; set; } = [];

    /// <summary>Greeting settings before the import.</summary>
    public sealed record GreetState(bool SendDm, string? DmText, ulong ByeChannel, bool SendBye, string? ByeText,
        string? AutoAssignRoles);

    /// <summary>XP settings before the import.</summary>
    public sealed record XpState(ulong LevelUpChannel, double Multiplier, bool ExclusiveRewards);

    /// <summary>A channel or role the import stopped from earning XP.</summary>
    public sealed record Exclusion(ulong Id, int Type);

    /// <summary>A level's reward role before the import, or null when it had none.</summary>
    public sealed record Reward(int Level, ulong? RoleId);

    /// <summary>Birthday settings before the import.</summary>
    public sealed record BirthdayState(ulong? Channel, ulong? Role, string? Message, int Features);

    /// <summary>Filter switches before the import.</summary>
    public sealed record FilterState(bool Words, bool Invites, bool Links, int WordWarn, int InviteWarn);

    /// <summary>Anti-spam or anti-mention settings before the import.</summary>
    public sealed record ProtectionState(bool Existed, int Threshold, int Action, int Minutes, ulong? RoleId,
        int Window, int MaxInWindow, bool IgnoreBots);

    /// <summary>A warning punishment before the import.</summary>
    public sealed record Punishment(int Count, int Action, int Time, ulong? RoleId);

    /// <summary>A text setting before the import, which may have been empty.</summary>
    public sealed record TextValue(string? Value);
}
