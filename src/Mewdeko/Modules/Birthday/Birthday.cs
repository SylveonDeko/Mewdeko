using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.Birthday.Services;
using Mewdeko.Modules.UserProfile.Common;

namespace Mewdeko.Modules.Birthday;

/// <summary>
///     Handles text commands for birthday announcements and management.
/// </summary>
public class Birthday : MewdekoModuleBase<BirthdayService>
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<Birthday> logger;

    /// <summary>
    ///     Initializes a new instance of the Birthday class.
    /// </summary>
    /// <param name="dbFactory">Database connection factory for data access.</param>
    /// <param name="logger">Logger</param>
    public Birthday(IDataConnectionFactory dbFactory, ILogger<Birthday> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Toggles birthday announcements for the user.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task BirthdayAnnouncements()
    {
        // Check if user has birthday set and privacy allows it
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(ctx.User);

        if (!user.Birthday.HasValue)
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayNoBirthdaySet(ctx.Guild.Id), Config);
            return;
        }

        if (user.ProfilePrivacy == (int)ProfilePrivacyEnum.Private)
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayProfilePrivate(ctx.Guild.Id), Config);
            return;
        }

        if (user.BirthdayDisplayMode == (int)BirthdayDisplayModeEnum.Disabled)
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayDisplayDisabled(ctx.Guild.Id), Config);
            return;
        }

        var enabled = await Service.ToggleBirthdayAnnouncementsAsync(ctx.User);

        if (enabled)
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayAnnouncementsEnabled(ctx.Guild.Id));
        else
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayAnnouncementsDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the user's timezone for birthday calculations.
    /// </summary>
    /// <param name="timezone">The timezone (e.g., "America/New_York", "Europe/London").</param>
    [Cmd]
    [Aliases]
    public async Task BirthdayTimezone([Remainder] string timezone)
    {
        var success = await Service.SetUserTimezoneAsync(ctx.User, timezone);

        if (success)
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayTimezoneSet(ctx.Guild.Id, timezone));
        else
            await ctx.Channel.SendErrorAsync(Strings.BirthdayTimezoneInvalid(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Shows upcoming birthdays in the server.
    /// </summary>
    /// <param name="days">Number of days to look ahead (default: 7, max: 30).</param>
    [Cmd]
    [Aliases]
    public async Task BirthdayList(int days = 7)
    {
        if (days is < 1 or > 30)
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayDaysRangeError(ctx.Guild.Id), Config);
            return;
        }

        var today = DateTime.UtcNow.Date;
        var birthdaysByDate = await Service.GetBirthdayUsersInRangeAsync(ctx.Guild.Id, today, days);

        if (!birthdaysByDate.Any())
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayNoUpcoming(ctx.Guild.Id, days), Config);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($":birthday: Upcoming Birthdays ({days} days)")
            .WithColor(Mewdeko.OkColor);

        var description = "";

        for (var i = 0; i < days; i++)
        {
            var checkDate = today.AddDays(i);

            if (!birthdaysByDate.TryGetValue(checkDate, out var dayBirthdays)) continue;
            var dayText = i == 0 ? "Today" : i == 1 ? "Tomorrow" : checkDate.ToString("MMM dd");
            description += $"\n**{dayText}**\n";

            foreach (var birthdayUser in dayBirthdays)
            {
                var guildUser = await ctx.Guild.GetUserAsync(birthdayUser.UserId);
                if (guildUser == null) continue;
                var age = birthdayUser.Birthday.HasValue &&
                          birthdayUser.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.MonthAndDate &&
                          birthdayUser.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.MonthOnly
                    ? $" (turning {DateTime.UtcNow.Year - birthdayUser.Birthday.Value.Year})"
                    : "";
                description += $"{Config.SuccessEmote} {guildUser.Mention}{age}\n";
            }
        }

        if (string.IsNullOrEmpty(description))
            description = "No birthdays found in the specified period.";

        embed.WithDescription(description);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows today's birthdays.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task BirthdayToday()
    {
        var birthdayUsers = await Service.GetBirthdayUsersForDateAsync(ctx.Guild.Id, DateTime.UtcNow.Date);

        if (!birthdayUsers.Any())
        {
            await ctx.Channel.SendErrorAsync(Strings.BirthdayNoToday(ctx.Guild.Id), Config);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.BirthdayTodayTitle(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor);

        var description = "";
        foreach (var birthdayUser in birthdayUsers)
        {
            var guildUser = await ctx.Guild.GetUserAsync(birthdayUser.UserId);
            if (guildUser != null)
            {
                var age = birthdayUser.Birthday.HasValue &&
                          birthdayUser.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.MonthAndDate &&
                          birthdayUser.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.MonthOnly
                    ? $" is turning {DateTime.UtcNow.Year - birthdayUser.Birthday.Value.Year}!"
                    : " is celebrating their birthday!";
                description += $"🎉 {guildUser.Mention}{age}\n";
            }
        }

        embed.WithDescription(description);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    #region Admin Commands

    /// <summary>
    ///     Sets the birthday announcement channel.
    /// </summary>
    /// <param name="channel">The channel to use for birthday announcements.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BirthdayChannel(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
        {
            config.BirthdayChannelId = channel.Id;
        });

        await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.Announcements);

        await ctx.Channel.SendConfirmAsync(Strings.BirthdayChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets the birthday role that users get on their birthday.
    /// </summary>
    /// <param name="role">The role to assign on birthdays.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task BirthdayRole(IRole? role = null)
    {
        if (role == null)
        {
            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayRoleId = null;
            });

            await Service.DisableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.BirthdayRole);
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayRoleDisabled(ctx.Guild.Id));
            return;
        }

        await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
        {
            config.BirthdayRoleId = role.Id;
        });

        await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.BirthdayRole);

        await ctx.Channel.SendConfirmAsync(Strings.BirthdayRoleSet(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    ///     Sets a custom birthday message template.
    /// </summary>
    /// <param name="message">The message template. Use {user} for mentions.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BirthdayMessage([Remainder] string? message = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            var config = await Service.GetBirthdayConfigAsync(ctx.Guild.Id);
            var currentMessage = config.BirthdayMessage ?? "🎉 Happy Birthday {user}! 🎂";
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayCurrentMessage(ctx.Guild.Id, currentMessage));
            return;
        }

        await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
        {
            config.BirthdayMessage = message;
        });

        await ctx.Channel.SendConfirmAsync(
            Strings.BirthdayMessageUpdated(ctx.Guild.Id, message.Replace("{user}", ctx.User.Mention)));
    }

    /// <summary>
    ///     Sets the role to ping when announcing birthdays.
    /// </summary>
    /// <param name="role">The role to ping, or null to disable.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BirthdayPingRole(IRole? role = null)
    {
        if (role == null)
        {
            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayPingRoleId = null;
            });

            await Service.DisableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.PingRole);
            await ctx.Channel.SendConfirmAsync(Strings.BirthdayPingDisabled(ctx.Guild.Id));
            return;
        }

        await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
        {
            config.BirthdayPingRoleId = role.Id;
        });

        await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.PingRole);

        await ctx.Channel.SendConfirmAsync(Strings.BirthdayPingEnabled(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    ///     Views the current birthday configuration.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task BirthdayConfig()
    {
        try
        {
            var config = await Service.GetBirthdayConfigAsync(ctx.Guild.Id);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.BirthdayConfigTitle(ctx.Guild.Id))
                .WithColor(Mewdeko.OkColor);

            var channel = config.BirthdayChannelId.HasValue
                ? (await ctx.Guild.GetTextChannelAsync(config.BirthdayChannelId.Value))?.Mention ?? "Not found"
                : "Not set";

            var role = config.BirthdayRoleId.HasValue
                ? ctx.Guild.GetRole(config.BirthdayRoleId.Value)?.Mention ?? "Not found"
                : "Not set";

            var pingRole = config.BirthdayPingRoleId.HasValue
                ? ctx.Guild.GetRole(config.BirthdayPingRoleId.Value)?.Mention ?? "Not found"
                : "Not set";

            var message = !config.BirthdayMessage.IsNullOrWhiteSpace()
                ? "Custom Message Set"
                : "🎉 Happy Birthday %user%! 🎂";

            var features = Enum.GetValues<BirthdayFeature>()
                .Where(f => f != BirthdayFeature.None && (config.EnabledFeatures & (int)f) != 0)
                .Select(f => f.ToString())
                .ToList();

            embed.AddField("Channel", channel, true)
                .AddField("Birthday Role", role, true)
                .AddField("Ping Role", pingRole, true)
                .AddField("Message Template", message)
                .AddField("Default Timezone", config.DefaultTimezone ?? "UTC", true)
                .AddField("Reminder Days", config.BirthdayReminderDays.ToString(), true)
                .AddField("Enabled Features", features.Any() ? string.Join(", ", features) : "None");

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in BirthdayConfig");
        }
    }

    #endregion
}