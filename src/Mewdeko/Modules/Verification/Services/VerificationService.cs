using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Verification.Services;

/// <summary>
///     Why a set of verification settings was refused.
/// </summary>
public enum VerificationError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The attempts limit is outside 1 to 10.
    /// </summary>
    InvalidAttempts,

    /// <summary>
    ///     The timeout is outside 1 to 60 minutes.
    /// </summary>
    InvalidTimeout,

    /// <summary>
    ///     The gate is on without a verification channel.
    /// </summary>
    MissingChannel,

    /// <summary>
    ///     The gate is on without an unverified or verified role.
    /// </summary>
    MissingRoles,

    /// <summary>
    ///     The unverified and verified roles are the same role.
    /// </summary>
    SameRole,

    /// <summary>
    ///     A role does not exist or cannot be given.
    /// </summary>
    InvalidRole,

    /// <summary>
    ///     A role is above the bot's highest role.
    /// </summary>
    RoleTooHigh,

    /// <summary>
    ///     The verified role is also an auto assign role, which would let everyone skip the captcha.
    /// </summary>
    AutoAssignConflict
}

/// <summary>
///     The result of a member asking for a captcha.
/// </summary>
public enum VerificationStart
{
    /// <summary>
    ///     The gate is off.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The member already passed.
    /// </summary>
    AlreadyVerified,

    /// <summary>
    ///     The member failed and has to wait.
    /// </summary>
    Locked,

    /// <summary>
    ///     A captcha was drawn.
    /// </summary>
    Ready
}

/// <summary>
///     The result of a member answering a captcha.
/// </summary>
public enum VerificationOutcome
{
    /// <summary>
    ///     The gate is off.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The member has no captcha to answer.
    /// </summary>
    NoChallenge,

    /// <summary>
    ///     The captcha ran out of time.
    /// </summary>
    Expired,

    /// <summary>
    ///     The answer was right and the roles were changed.
    /// </summary>
    Passed,

    /// <summary>
    ///     The answer was right but the roles could not be changed.
    /// </summary>
    RoleError,

    /// <summary>
    ///     The answer was wrong and the member can try again.
    /// </summary>
    Wrong,

    /// <summary>
    ///     The member is out of attempts and has to wait.
    /// </summary>
    Locked,

    /// <summary>
    ///     The member is out of attempts and was kicked.
    /// </summary>
    Kicked
}

/// <summary>
///     A drawn captcha.
/// </summary>
/// <param name="Result">Whether a captcha was drawn.</param>
/// <param name="Image">The PNG, when one was drawn.</param>
/// <param name="Minutes">How long the captcha is valid, or how long the member has to wait.</param>
/// <param name="AttemptsLeft">How many wrong answers the member has left.</param>
public record VerificationStartResult(VerificationStart Result, byte[]? Image, int Minutes, int AttemptsLeft);

/// <summary>
///     A channel overwrite the gate changes.
/// </summary>
/// <param name="ChannelId">The channel.</param>
/// <param name="ChannelName">The channel name.</param>
/// <param name="RoleId">The role the overwrite is for.</param>
/// <param name="Allow">The allowed permissions after the change.</param>
/// <param name="Deny">The denied permissions after the change.</param>
/// <param name="HadOverwrite">Whether the role had an overwrite on the channel.</param>
/// <param name="PreviousAllow">The allowed permissions before the change.</param>
/// <param name="PreviousDeny">The denied permissions before the change.</param>
public record VerificationPermissionChange(
    ulong ChannelId,
    string ChannelName,
    ulong RoleId,
    ulong Allow,
    ulong Deny,
    bool HadOverwrite,
    ulong PreviousAllow,
    ulong PreviousDeny);

/// <summary>
///     Makes new members pass a captcha before they get access.
/// </summary>
public class VerificationService : INService
{
    /// <summary>
    ///     The ID of the button on the verification message.
    /// </summary>
    public const string StartButtonId = "verification_start";

    private const ulong GatePermissions = 1UL << 10 | 1UL << 16;
    private const ulong ViewChannel = 1UL << 10;
    private const ulong SendMessages = 1UL << 11;

    private readonly AutoAssignRoleService autoAssign;
    private readonly ConcurrentDictionary<(ulong, ulong), Challenge> challenges = new();
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<VerificationService> logger;
    private readonly ConcurrentDictionary<ulong, VerificationSetting> settings = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VerificationService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="autoAssign">The auto assign role service, so roles are not handled twice.</param>
    public VerificationService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, GeneratedBotStrings strings, ILogger<VerificationService> logger,
        AutoAssignRoleService autoAssign)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
        this.autoAssign = autoAssign;
        eventHandler.Subscribe("UserJoined", "VerificationService", OnUserJoined);
        eventHandler.Subscribe("UserLeft", "VerificationService", OnUserLeft);
    }

    /// <summary>
    ///     Gets a guild's settings, or the defaults when it has none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<VerificationSetting> GetSettingsAsync(ulong guildId)
    {
        if (settings.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.VerificationSettings.FirstOrDefaultAsync(x => x.GuildId == guildId)
                  ?? new VerificationSetting
                  {
                      GuildId = guildId, DateAdded = DateTime.UtcNow
                  };
        settings[guildId] = row;
        return row;
    }

    /// <summary>
    ///     Changes a guild's settings. Turning the gate off puts back the channel permissions it changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="change">Applies the change to a copy of the settings.</param>
    /// <returns>The error, or none, the settings as they are now, and how many channel permissions were restored.</returns>
    public async Task<(VerificationError Error, VerificationSetting Setting, int Restored)> UpdateAsync(ulong guildId,
        Action<VerificationSetting> change)
    {
        var current = await GetSettingsAsync(guildId);
        var copy = Clone(current);
        change(copy);

        var error = await ValidateAsync(copy, current);
        if (error != VerificationError.None)
            return (error, current, 0);

        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.VerificationSettings.AnyAsync(x => x.GuildId == guildId))
            await db.UpdateAsync(copy);
        else
            await db.InsertAsync(copy);

        settings[guildId] = copy;

        var restored = 0;
        if (current.Enabled && !copy.Enabled)
            restored = (await RestorePermissionsAsync(guildId)).Restored;

        return (VerificationError.None, copy, restored);
    }

    /// <summary>
    ///     Makes sure a verification channel exists and posts the verification message in it.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel to use, or null to use the saved one or create one.</param>
    /// <returns>The channel, or null when it could not be created or written to.</returns>
    public async Task<ITextChannel?> SetupAsync(IGuild guild, ITextChannel? channel)
    {
        try
        {
            var setting = await GetSettingsAsync(guild.Id);
            channel ??= setting.ChannelId is { } saved ? await guild.GetTextChannelAsync(saved) : null;
            channel ??= await guild.CreateTextChannelAsync("verify");

            var (error, _, _) = await UpdateAsync(guild.Id, x => x.ChannelId = channel.Id);
            return error == VerificationError.None && await PostPanelAsync(guild.Id) ? channel : null;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Could not set up verification in {GuildId}", guild.Id);
            return null;
        }
    }

    /// <summary>
    ///     Posts the verification message, replacing the old one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Whether the message was posted.</returns>
    public async Task<bool> PostPanelAsync(ulong guildId)
    {
        var setting = await GetSettingsAsync(guildId);
        var guild = client.GetGuild(guildId);
        if (guild is null || setting.ChannelId is not { } channelId || guild.GetTextChannel(channelId) is not { } channel)
            return false;

        try
        {
            if (setting.MessageId is { } old)
                await channel.DeleteMessageAsync(old);
        }
        catch (HttpException)
        {
        }

        try
        {
            var message = await channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(strings.VerificationPanelTitle(guildId))
                .WithDescription(strings.VerificationPanelBody(guildId))
                .Build(), components: new ComponentBuilder()
                .WithButton(strings.VerificationPanelButton(guildId), StartButtonId, ButtonStyle.Success)
                .Build());
            await UpdateAsync(guildId, x => x.MessageId = message.Id);
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Could not post the verification message in {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    ///     Works out which channel overwrites the gate needs. Nothing is changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<List<VerificationPermissionChange>> PlanPermissionsAsync(ulong guildId)
    {
        var plan = new List<VerificationPermissionChange>();
        var setting = await GetSettingsAsync(guildId);
        var guild = client.GetGuild(guildId);
        if (guild is null || setting.ChannelId is not { } gateId ||
            (setting.UnverifiedRoleId is null && setting.VerifiedRoleId is null))
            return plan;

        foreach (var channel in guild.Channels.Where(x => x is not SocketThreadChannel))
        {
            var isGate = channel.Id == gateId;
            var everyone = channel.GetPermissionOverwrite(guild.EveryoneRole);
            var canView = everyone?.ViewChannel switch
            {
                PermValue.Allow => true,
                PermValue.Deny => false,
                _ => guild.EveryoneRole.Permissions.ViewChannel
            };
            if (!canView && !isGate)
                continue;

            if (setting.UnverifiedRoleId is { } unverified)
            {
                Plan(plan, channel, guild, unverified, isGate ? GatePermissions : 0, isGate ? SendMessages : ViewChannel);
            }
            else if (setting.VerifiedRoleId is { } verified)
            {
                if (isGate)
                {
                    Plan(plan, channel, guild, guild.Id, GatePermissions, SendMessages);
                    Plan(plan, channel, guild, verified, 0, ViewChannel);
                }
                else
                {
                    Plan(plan, channel, guild, guild.Id, 0, ViewChannel);
                    Plan(plan, channel, guild, verified, ViewChannel, 0);
                }
            }
        }

        return plan;
    }

    /// <summary>
    ///     Changes the channel overwrites the gate needs, remembering what they were.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many overwrites were changed and how many could not be.</returns>
    public async Task<(int Changed, int Failed)> ApplyPermissionsAsync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return (0, 0);

        var plan = await PlanPermissionsAsync(guildId);
        await using var db = await dbFactory.CreateConnectionAsync();
        var changed = 0;
        var failed = 0;

        foreach (var item in plan)
        {
            var channel = guild.GetChannel(item.ChannelId);
            var role = guild.GetRole(item.RoleId);
            if (channel is null || role is null)
                continue;

            var hasBackup = await db.VerificationPermissionBackups.AnyAsync(x =>
                x.GuildId == guildId && x.ChannelId == item.ChannelId && x.RoleId == item.RoleId);
            if (!hasBackup)
            {
                await db.InsertAsync(new VerificationPermissionBackup
                {
                    GuildId = guildId,
                    ChannelId = item.ChannelId,
                    RoleId = item.RoleId,
                    HadOverwrite = item.HadOverwrite,
                    PreviousAllow = item.PreviousAllow,
                    PreviousDeny = item.PreviousDeny
                });
            }

            try
            {
                await channel.AddPermissionOverwriteAsync(role, new OverwritePermissions(item.Allow, item.Deny));
                changed++;
            }
            catch (HttpException ex)
            {
                failed++;
                logger.LogWarning(ex, "Could not change permissions on {ChannelId} in {GuildId}", item.ChannelId,
                    guildId);
                if (!hasBackup)
                {
                    await db.VerificationPermissionBackups.Where(x =>
                            x.GuildId == guildId && x.ChannelId == item.ChannelId && x.RoleId == item.RoleId)
                        .DeleteAsync();
                }
            }
        }

        return (changed, failed);
    }

    /// <summary>
    ///     Puts back the channel overwrites the gate changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many overwrites were restored and how many could not be.</returns>
    public async Task<(int Restored, int Failed)> RestorePermissionsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var backups = await db.VerificationPermissionBackups.Where(x => x.GuildId == guildId).ToListAsync();
        var guild = client.GetGuild(guildId);
        var restored = 0;
        var failed = 0;

        foreach (var backup in backups)
        {
            var channel = guild?.GetChannel(backup.ChannelId);
            var role = guild?.GetRole(backup.RoleId);
            if (channel is not null && role is not null)
            {
                try
                {
                    if (backup.HadOverwrite)
                    {
                        await channel.AddPermissionOverwriteAsync(role,
                            new OverwritePermissions(backup.PreviousAllow, backup.PreviousDeny));
                    }
                    else
                    {
                        await channel.RemovePermissionOverwriteAsync(role);
                    }

                    restored++;
                }
                catch (HttpException ex)
                {
                    failed++;
                    logger.LogWarning(ex, "Could not restore permissions on {ChannelId} in {GuildId}",
                        backup.ChannelId, guildId);
                    continue;
                }
            }

            await db.VerificationPermissionBackups.Where(x => x.Id == backup.Id).DeleteAsync();
        }

        return (restored, failed);
    }

    /// <summary>
    ///     Draws a captcha for a member.
    /// </summary>
    /// <param name="user">The member.</param>
    public async Task<VerificationStartResult> StartAsync(IGuildUser user)
    {
        var setting = await GetSettingsAsync(user.GuildId);
        if (!setting.Enabled)
            return new VerificationStartResult(VerificationStart.Disabled, null, 0, 0);
        if (IsVerified(setting, user))
            return new VerificationStartResult(VerificationStart.AlreadyVerified, null, 0, 0);

        var challenge = challenges.GetOrAdd((user.GuildId, user.Id), _ => new Challenge());
        string code;
        int attemptsLeft;
        lock (challenge)
        {
            var now = DateTime.UtcNow;
            if (challenge.LockedUntil is { } until)
            {
                if (until > now)
                {
                    return new VerificationStartResult(VerificationStart.Locked, null,
                        (int)Math.Ceiling((until - now).TotalMinutes), 0);
                }

                challenge.LockedUntil = null;
                challenge.Failed = 0;
            }

            if (challenge.ExpiresAt < now)
                challenge.Failed = 0;

            code = VerificationCaptcha.NewCode();
            challenge.Code = code;
            challenge.ExpiresAt = now.AddMinutes(setting.TimeoutMinutes);
            attemptsLeft = Math.Max(1, setting.MaxAttempts - challenge.Failed);
        }

        var image = await Task.Run(() => VerificationCaptcha.Render(code));
        return new VerificationStartResult(VerificationStart.Ready, image, setting.TimeoutMinutes, attemptsLeft);
    }

    /// <summary>
    ///     Checks a member's answer and acts on it.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="answer">What the member typed.</param>
    /// <returns>The outcome, how many attempts are left and how many minutes a locked member has to wait.</returns>
    public async Task<(VerificationOutcome Outcome, int AttemptsLeft, int Minutes)> SubmitAsync(IGuildUser user,
        string? answer)
    {
        var setting = await GetSettingsAsync(user.GuildId);
        if (!setting.Enabled)
            return (VerificationOutcome.Disabled, 0, 0);
        if (!challenges.TryGetValue((user.GuildId, user.Id), out var challenge))
            return (VerificationOutcome.NoChallenge, 0, 0);

        bool passed;
        bool outOfAttempts;
        int attemptsLeft;
        lock (challenge)
        {
            var now = DateTime.UtcNow;
            if (challenge.LockedUntil is { } until && until > now)
                return (VerificationOutcome.Locked, 0, (int)Math.Ceiling((until - now).TotalMinutes));
            if (challenge.Code is null)
                return (VerificationOutcome.NoChallenge, 0, 0);
            if (challenge.ExpiresAt < now)
            {
                challenge.Code = null;
                return (VerificationOutcome.Expired, 0, 0);
            }

            passed = VerificationCaptcha.Matches(challenge.Code, answer);
            if (!passed)
                challenge.Failed++;

            outOfAttempts = !passed && challenge.Failed >= setting.MaxAttempts;
            attemptsLeft = Math.Max(0, setting.MaxAttempts - challenge.Failed);
            if (outOfAttempts)
            {
                challenge.Code = null;
                challenge.LockedUntil = now.AddMinutes(setting.TimeoutMinutes);
            }
        }

        if (passed)
        {
            challenges.TryRemove((user.GuildId, user.Id), out _);
            if (!await GrantAsync(setting, user))
                return (VerificationOutcome.RoleError, 0, 0);

            await LogAsync(user.GuildId, setting, id => strings.VerificationLogPassed(id, user.Id));
            return (VerificationOutcome.Passed, 0, 0);
        }

        if (!outOfAttempts)
            return (VerificationOutcome.Wrong, attemptsLeft, 0);

        if (setting.KickOnFail)
        {
            try
            {
                await user.KickAsync(strings.VerificationKickReason(user.GuildId));
                challenges.TryRemove((user.GuildId, user.Id), out _);
                await LogAsync(user.GuildId, setting, id => strings.VerificationLogKicked(id, user.Id));
                return (VerificationOutcome.Kicked, 0, 0);
            }
            catch (HttpException ex)
            {
                logger.LogWarning(ex, "Could not kick {UserId} from {GuildId}", user.Id, user.GuildId);
            }
        }

        await LogAsync(user.GuildId, setting, id => strings.VerificationLogFailed(id, user.Id));
        return (VerificationOutcome.Locked, 0, setting.TimeoutMinutes);
    }

    /// <summary>
    ///     Verifies the members already in a guild without a captcha, in the background.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many members will be verified, or null when the guild is not available.</returns>
    public async Task<int?> VerifyExistingAsync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return null;

        var setting = await GetSettingsAsync(guildId);
        await guild.DownloadUsersAsync();
        var targets = guild.Users.Where(x => !x.IsBot && NeedsGrant(setting, x)).Select(x => x.Id).ToList();

        _ = Task.Run(async () =>
        {
            var done = 0;
            foreach (var id in targets)
            {
                var member = guild.GetUser(id);
                if (member is not null && await GrantAsync(setting, member))
                    done++;
            }

            await LogAsync(guildId, setting, gid => strings.VerificationLogBulk(gid, done, targets.Count));
        });

        return targets.Count;
    }

    /// <summary>
    ///     Gets the member count the bulk verification would cover.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<int> CountExistingAsync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return 0;

        var setting = await GetSettingsAsync(guildId);
        return guild.Users.Count(x => !x.IsBot && NeedsGrant(setting, x));
    }

    /// <summary>
    ///     Writes why settings were refused.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="error">The error.</param>
    public string DescribeError(ulong guildId, VerificationError error) => error switch
    {
        VerificationError.InvalidAttempts => strings.VerificationInvalidAttempts(guildId),
        VerificationError.InvalidTimeout => strings.VerificationInvalidTimeout(guildId),
        VerificationError.MissingChannel => strings.VerificationMissingChannel(guildId),
        VerificationError.MissingRoles => strings.VerificationMissingRoles(guildId),
        VerificationError.SameRole => strings.VerificationSameRole(guildId),
        VerificationError.InvalidRole => strings.VerificationInvalidRole(guildId),
        VerificationError.RoleTooHigh => strings.VerificationRoleTooHigh(guildId),
        _ => strings.VerificationAutoAssignConflict(guildId)
    };

    /// <summary>
    ///     Writes what the permission changes would do, for a preview.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="plan">The planned changes.</param>
    public string DescribePlan(ulong guildId, List<VerificationPermissionChange> plan)
    {
        var lines = plan.Take(15).Select(x => strings.VerificationPreviewLine(guildId, x.ChannelId, x.RoleId));
        var text = strings.VerificationPreviewBody(guildId, plan.Count, string.Join('\n', lines));
        return plan.Count > 15 ? $"{text}\n{strings.VerificationPreviewMore(guildId, plan.Count - 15)}" : text;
    }

    /// <summary>
    ///     Builds the embed showing a guild's settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="setting">The settings.</param>
    public EmbedBuilder BuildSummary(ulong guildId, VerificationSetting setting)
    {
        var none = strings.VerificationNone(guildId);
        var onOff = (bool value) => value ? strings.VerificationOn(guildId) : strings.VerificationOff(guildId);
        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.VerificationShowTitle(guildId))
            .WithDescription(strings.VerificationShowBody(guildId,
                onOff(setting.Enabled),
                setting.ChannelId is { } channel ? $"<#{channel}>" : none,
                setting.UnverifiedRoleId is { } unverified ? $"<@&{unverified}>" : none,
                setting.VerifiedRoleId is { } verified ? $"<@&{verified}>" : none,
                setting.MaxAttempts,
                setting.TimeoutMinutes,
                onOff(setting.KickOnFail),
                setting.LogChannelId is { } log ? $"<#{log}>" : none));
    }

    private static void Plan(List<VerificationPermissionChange> plan, SocketGuildChannel channel, SocketGuild guild,
        ulong roleId, ulong addAllow, ulong addDeny)
    {
        var role = guild.GetRole(roleId);
        if (role is null)
            return;

        var existing = channel.GetPermissionOverwrite(role);
        var previousAllow = existing?.AllowValue ?? 0;
        var previousDeny = existing?.DenyValue ?? 0;
        var allow = (previousAllow | addAllow) & ~addDeny;
        var deny = (previousDeny | addDeny) & ~addAllow;
        if (allow == previousAllow && deny == previousDeny)
            return;

        plan.Add(new VerificationPermissionChange(channel.Id, channel.Name, roleId, allow, deny, existing.HasValue,
            previousAllow, previousDeny));
    }

    private static bool IsVerified(VerificationSetting setting, IGuildUser user)
    {
        if (setting.VerifiedRoleId is { } verified)
            return user.RoleIds.Contains(verified);

        return setting.UnverifiedRoleId is not { } unverified || !user.RoleIds.Contains(unverified);
    }

    private static bool NeedsGrant(VerificationSetting setting, IGuildUser user) =>
        setting.VerifiedRoleId is { } verified && !user.RoleIds.Contains(verified)
        || setting.UnverifiedRoleId is { } unverified && user.RoleIds.Contains(unverified);

    private async Task<bool> GrantAsync(VerificationSetting setting, IGuildUser user)
    {
        try
        {
            if (setting.VerifiedRoleId is { } verified && !user.RoleIds.Contains(verified))
                await user.AddRoleAsync(verified);
            if (setting.UnverifiedRoleId is { } unverified && user.RoleIds.Contains(unverified))
                await user.RemoveRoleAsync(unverified);
            return true;
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Could not change roles for {UserId} in {GuildId}", user.Id, user.GuildId);
            return false;
        }
    }

    private async Task LogAsync(ulong guildId, VerificationSetting setting, Func<ulong, string> text)
    {
        if (setting.LogChannelId is not { } logId || client.GetGuild(guildId)?.GetTextChannel(logId) is not { } channel)
            return;

        try
        {
            await channel.SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text(guildId)).Build(),
                allowedMentions: AllowedMentions.None);
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Could not write to the verification log in {GuildId}", guildId);
        }
    }

    private async Task<VerificationError> ValidateAsync(VerificationSetting setting, VerificationSetting current)
    {
        if (setting.MaxAttempts is < 1 or > 10)
            return VerificationError.InvalidAttempts;
        if (setting.TimeoutMinutes is < 1 or > 60)
            return VerificationError.InvalidTimeout;
        if (setting.UnverifiedRoleId is { } same && same == setting.VerifiedRoleId)
            return VerificationError.SameRole;
        if (setting.Enabled && setting.ChannelId is null)
            return VerificationError.MissingChannel;
        if (setting.Enabled && setting.UnverifiedRoleId is null && setting.VerifiedRoleId is null)
            return VerificationError.MissingRoles;

        var guild = client.GetGuild(setting.GuildId);
        if (guild is null)
            return VerificationError.None;

        var top = guild.CurrentUser.Roles.Max(x => x.Position);
        foreach (var (id, previous) in new[]
                 {
                     (setting.UnverifiedRoleId, current.UnverifiedRoleId), (setting.VerifiedRoleId, current.VerifiedRoleId)
                 })
        {
            if (id is not { } roleId || roleId == previous)
                continue;

            var role = guild.GetRole(roleId);
            if (role is null || role.IsManaged || role.Id == guild.Id)
                return VerificationError.InvalidRole;
            if (role.Position >= top)
                return VerificationError.RoleTooHigh;
        }

        if (setting.VerifiedRoleId is { } verified && setting.VerifiedRoleId != current.VerifiedRoleId
                                                   && (await autoAssign.TryGetNormalRoles(setting.GuildId)).Contains(verified))
            return VerificationError.AutoAssignConflict;

        return VerificationError.None;
    }

    private async Task OnUserJoined(IGuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            var setting = await GetSettingsAsync(user.GuildId);
            if (!setting.Enabled || setting.UnverifiedRoleId is not { } roleId || user.RoleIds.Contains(roleId)
                || setting.VerifiedRoleId is { } verified && user.RoleIds.Contains(verified))
                return;

            if ((await autoAssign.TryGetNormalRoles(user.GuildId)).Contains(roleId))
                return;

            await user.AddRoleAsync(roleId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not give the unverified role to {UserId} in {GuildId}", user.Id, user.GuildId);
        }
    }

    private Task OnUserLeft(IGuild guild, IUser user)
    {
        challenges.TryRemove((guild.Id, user.Id), out _);
        return Task.CompletedTask;
    }

    private static VerificationSetting Clone(VerificationSetting source) => new()
    {
        Id = source.Id,
        GuildId = source.GuildId,
        Enabled = source.Enabled,
        ChannelId = source.ChannelId,
        MessageId = source.MessageId,
        UnverifiedRoleId = source.UnverifiedRoleId,
        VerifiedRoleId = source.VerifiedRoleId,
        MaxAttempts = source.MaxAttempts,
        TimeoutMinutes = source.TimeoutMinutes,
        KickOnFail = source.KickOnFail,
        LogChannelId = source.LogChannelId,
        DateAdded = source.DateAdded
    };

    private sealed class Challenge
    {
        public string? Code { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Failed { get; set; }
        public DateTime? LockedUntil { get; set; }
    }
}
