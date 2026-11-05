using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.Birthday.Services;
using Mewdeko.Modules.UserProfile.Common;

namespace Mewdeko.Modules.Birthday;

/// <summary>
///     Provides slash commands for birthday announcements and management within the Mewdeko bot framework.
/// </summary>
[Group("birthday", "Commands to manage birthday announcements and settings")]
public class SlashBirthday : MewdekoSlashModuleBase<BirthdayService>
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the SlashBirthday class.
    /// </summary>
    /// <param name="dbFactory">Database connection factory for data access.</param>
    public SlashBirthday(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Toggles birthday announcements for the user.
    /// </summary>
    [SlashCommand("announcements", "Toggle birthday announcements for yourself")]
    public async Task BirthdayAnnouncements()
    {
        // Check if user has birthday set and privacy allows it
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(ctx.User);

        if (!user.Birthday.HasValue)
        {
            await ctx.Interaction.SendErrorAsync(Strings.BirthdayNoBirthdaySet(ctx.Guild.Id), Config);
            return;
        }

        if (user.ProfilePrivacy == (int)ProfilePrivacyEnum.Private)
        {
            await ctx.Interaction.SendErrorAsync(Strings.BirthdayProfilePrivate(ctx.Guild.Id), Config);
            return;
        }

        if (user.BirthdayDisplayMode == (int)BirthdayDisplayModeEnum.Disabled)
        {
            await ctx.Interaction.SendErrorAsync(Strings.BirthdayDisplayDisabled(ctx.Guild.Id), Config);
            return;
        }

        var enabled = await Service.ToggleBirthdayAnnouncementsAsync(ctx.User);

        if (enabled)
            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayAnnouncementsEnabled(ctx.Guild.Id));
        else
            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayAnnouncementsDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the user's timezone for birthday calculations.
    /// </summary>
    /// <param name="timezone">The timezone (e.g., "America/New_York", "Europe/London").</param>
    [SlashCommand("timezone", "Set your timezone for accurate birthday detection")]
    public async Task BirthdayTimezone(
        [Summary("timezone", "Your timezone (e.g., America/New_York, Europe/London)")]
        string timezone)
    {
        var success = await Service.SetUserTimezoneAsync(ctx.User, timezone);

        if (success)
            await ctx.Interaction.SendConfirmAsync(
                Strings.BirthdayTimezoneSet(ctx.Guild.Id, timezone));
        else
            await ctx.Interaction.SendErrorAsync(
                Strings.BirthdayInvalidTimezone(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Shows upcoming birthdays in the server.
    /// </summary>
    /// <param name="days">Number of days to look ahead (default: 7, max: 30).</param>
    [SlashCommand("list", "Show upcoming birthdays in the server")]
    public async Task BirthdayList(
        [Summary("days", "Number of days to look ahead (1-30)")] [MinValue(1)] [MaxValue(30)]
        int days = 7)
    {
        var birthdayUsers = await Service.GetBirthdayUsersForDateAsync(ctx.Guild.Id, DateTime.UtcNow.Date);

        if (!birthdayUsers.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.BirthdayNoUpcoming(ctx.Guild.Id, days), Config);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🎂 Upcoming Birthdays ({days} days)")
            .WithColor(Mewdeko.OkColor);

        var today = DateTime.UtcNow.Date;
        var description = "";
        var yearRule = (await Service.GetBirthdayConfigAsync(ctx.Guild.Id)).BirthYearRule;

        for (var i = 0; i < days; i++)
        {
            var checkDate = today.AddDays(i);
            var dayBirthdays = await Service.GetBirthdayUsersForDateAsync(ctx.Guild.Id, checkDate);

            if (dayBirthdays.Any())
            {
                var dayText = i == 0 ? "Today" : i == 1 ? "Tomorrow" : checkDate.ToString("MMM dd");
                description += $"\n**{dayText}**\n";

                foreach (var birthdayUser in dayBirthdays)
                {
                    var guildUser = await ctx.Guild.GetUserAsync(birthdayUser.UserId);
                    if (guildUser != null)
                    {
                        var age = BirthdayService.SharesBirthYear(birthdayUser, yearRule)
                            ? $" (turning {DateTime.UtcNow.Year - birthdayUser.Birthday!.Value.Year})"
                            : "";
                        description += $"🎉 {guildUser.Mention}{age}\n";
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(description))
            description = "No birthdays found in the specified period.";

        embed.WithDescription(description);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows today's birthdays.
    /// </summary>
    [SlashCommand("today", "Show today's birthdays")]
    public async Task BirthdayToday()
    {
        var birthdayUsers = await Service.GetBirthdayUsersForDateAsync(ctx.Guild.Id, DateTime.UtcNow.Date);

        if (!birthdayUsers.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.BirthdayNoToday(ctx.Guild.Id), Config);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.BirthdayTodayTitle(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor);

        var description = "";
        var yearRule = (await Service.GetBirthdayConfigAsync(ctx.Guild.Id)).BirthYearRule;
        foreach (var birthdayUser in birthdayUsers)
        {
            var guildUser = await ctx.Guild.GetUserAsync(birthdayUser.UserId);
            if (guildUser != null)
            {
                var age = BirthdayService.SharesBirthYear(birthdayUser, yearRule)
                    ? $" is turning {DateTime.UtcNow.Year - birthdayUser.Birthday!.Value.Year}!"
                    : " is celebrating their birthday!";
                description += $"🎉 {guildUser.Mention}{age}\n";
            }
        }

        embed.WithDescription(description);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    #region Admin Commands

    /// <summary>
    ///     Admin commands for configuring birthday settings.
    /// </summary>
    [Group("config", "Configure birthday settings for the server")]
    public class BirthdayConfig : MewdekoSlashSubmodule<BirthdayService>
    {
        /// <summary>
        ///     Sets the birthday announcement channel.
        /// </summary>
        /// <param name="channel">The channel to use for birthday announcements.</param>
        [SlashCommand("channel", "Set the channel for birthday announcements")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Channel(
            [Summary("channel", "The channel for birthday announcements")]
            ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayChannelId = channel.Id;
            });

            await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.Announcements);

            await ctx.Interaction.SendConfirmAsync(
                Strings.BirthdayChannelSetMsg(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets the birthday role that users get on their birthday.
        /// </summary>
        /// <param name="role">The role to assign on birthdays.</param>
        [SlashCommand("role", "Set the role users get on their birthday")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Role(
            [Summary("role", "The role to assign on birthdays (leave empty to disable)")]
            IRole? role = null)
        {
            if (role == null)
            {
                await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
                {
                    config.BirthdayRoleId = null;
                });

                await Service.DisableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.BirthdayRole);
                await ctx.Interaction.SendConfirmAsync(Strings.BirthdayRoleDisabledMsg(ctx.Guild.Id));
                return;
            }

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayRoleId = role.Id;
            });

            await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.BirthdayRole);

            await ctx.Interaction.SendConfirmAsync(
                Strings.BirthdayRoleSetMsg(ctx.Guild.Id, role.Mention));
        }

        /// <summary>
        ///     Sets a custom birthday message template.
        /// </summary>
        /// <param name="message">The message template. Use {user} for mentions.</param>
        [SlashCommand("message", "Set a custom birthday message template")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Message(
            [Summary("message", "The message template (use %user% for mentions, leave empty to view current)")]
            [MaxLength(2000)]
            string? message = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                var config = await Service.GetBirthdayConfigAsync(ctx.Guild.Id);
                var currentMessage = config.BirthdayMessage ?? "🎉 Happy Birthday %user%! 🎂";
                await ctx.Interaction.SendConfirmAsync(Strings.BirthdayCurrentMessageMsg(ctx.Guild.Id, currentMessage));
                return;
            }

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayMessage = message;
            });

            await ctx.Interaction.SendConfirmAsync(
                Strings.BirthdayMessageUpdated(ctx.Guild.Id, message.Replace("%user%", ctx.User.Mention)));
        }

        /// <summary>
        ///     Sets the role to ping when announcing birthdays.
        /// </summary>
        /// <param name="role">The role to ping, or null to disable.</param>
        [SlashCommand("pingrole", "Set a role to ping for birthday announcements")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PingRole(
            [Summary("role", "The role to ping for announcements (leave empty to disable)")]
            IRole? role = null)
        {
            if (role == null)
            {
                await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
                {
                    config.BirthdayPingRoleId = null;
                });

                await Service.DisableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.PingRole);
                await ctx.Interaction.SendConfirmAsync(Strings.BirthdayPingDisabled(ctx.Guild.Id));
                return;
            }

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayPingRoleId = role.Id;
            });

            await Service.EnableBirthdayFeatureAsync(ctx.Guild.Id, BirthdayFeature.PingRole);

            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayPingEnabled(ctx.Guild.Id, role.Mention));
        }

        /// <summary>
        ///     Views the current birthday configuration.
        /// </summary>
        [SlashCommand("view", "View the current birthday configuration")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task View()
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

            var message = !config.BirthdayMessage.IsNullOrWhiteSpace() ? "Custom Message Set" : "🎉 Happy Birthday %user%! 🎂";

            var features = Enum.GetValues<BirthdayFeature>()
                .Where(f => f != BirthdayFeature.None && (config.EnabledFeatures & (int)f) != 0)
                .Select(f => f.ToString())
                .ToList();

            embed.AddField("Channel", channel, true)
                .AddField("Birthday Role", role, true)
                .AddField("Ping Role", pingRole, true)
                .AddField("Message Template", message)
                .AddField("Age Message", config.AgeMessage.IsNullOrWhiteSpace() ? "Not set" : "Custom Message Set")
                .AddField("Announcement Hour", $"{config.AnnouncementHour}:00", true)
                .AddField("Birth Year", ((BirthYearRule)config.BirthYearRule).ToString(), true)
                .AddField("Default Timezone", config.DefaultTimezone ?? "UTC", true)
                .AddField("Reminder Days", config.BirthdayReminderDays.ToString(), true)
                .AddField("Enabled Features", features.Any() ? string.Join(", ", features) : "None");

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Resets the birthday configuration to defaults.
        /// </summary>
        [SlashCommand("reset", "Reset birthday configuration to defaults")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Reset()
        {
            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthdayChannelId = null;
                config.BirthdayRoleId = null;
                config.BirthdayMessage = null;
                config.BirthdayPingRoleId = null;
                config.BirthdayReminderDays = 0;
                config.DefaultTimezone = "UTC";
                config.EnabledFeatures = 0;
                config.AnnouncementHour = 0;
                config.AgeMessage = null;
                config.BirthYearRule = (int)BirthYearRule.Optional;
            });

            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayConfigReset(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the hour of the day birthdays are announced, in the server's timezone.
        /// </summary>
        /// <param name="hour">The hour from 0 to 23.</param>
        [SlashCommand("hour", "Set the hour birthdays are announced, in the server timezone")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Hour([Summary("hour", "The hour from 0 to 23")] [MinValue(0)] [MaxValue(23)] int hour)
        {
            if (hour is < 0 or > 23)
            {
                await ctx.Interaction.SendErrorAsync(Strings.BirthdayHourInvalid(ctx.Guild.Id), Config);
                return;
            }

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.AnnouncementHour = hour;
            });

            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayHourSet(ctx.Guild.Id, hour));
        }

        /// <summary>
        ///     Sets the message announced for members who shared their birth year.
        /// </summary>
        /// <param name="message">The message. %birthday.age% is the member's age. Leave empty to clear it.</param>
        [SlashCommand("age-message", "Set the message announced for members who shared their birth year")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AgeMessage(
            [Summary("message", "Uses %birthday.age% for the age, leave empty to clear")]
            string? message = null)
        {
            var text = string.IsNullOrWhiteSpace(message) ? null : message;

            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.AgeMessage = text;
            });

            await ctx.Interaction.SendConfirmAsync(text is null
                ? Strings.BirthdayAgeMessageCleared(ctx.Guild.Id)
                : Strings.BirthdayAgeMessageSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets whether members may share their birth year.
        /// </summary>
        /// <param name="rule">NotAllowed, Optional or Required.</param>
        [SlashCommand("year-rule", "Set whether members may share their birth year")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task YearRule([Summary("rule", "Not allowed, optional or required")] BirthYearRule rule)
        {
            await Service.UpdateBirthdayConfigAsync(ctx.Guild.Id, config =>
            {
                config.BirthYearRule = (int)rule;
            });

            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayYearRuleSet(ctx.Guild.Id, rule.ToString()));
        }

        /// <summary>
        ///     Sets a member's birthday.
        /// </summary>
        /// <param name="user">The member.</param>
        /// <param name="date">The birthday.</param>
        [SlashCommand("set-member", "Set a member's birthday")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetMember([Summary("user", "The member")] IUser user,
            [Summary("date", "The birthday, such as 2000-05-14")]
            DateTime date)
        {
            await Service.SetUserBirthdayAsync(user, date);
            await ctx.Interaction.SendConfirmAsync(
                Strings.BirthdayAdminSet(ctx.Guild.Id, user.Mention, date.ToString("d")));
        }

        /// <summary>
        ///     Removes a member's birthday.
        /// </summary>
        /// <param name="user">The member.</param>
        [SlashCommand("remove-member", "Remove a member's birthday")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RemoveMember([Summary("user", "The member")] IUser user)
        {
            if (!await Service.ClearUserBirthdayAsync(user, false))
            {
                await ctx.Interaction.SendErrorAsync(Strings.BirthdayAdminNone(ctx.Guild.Id, user.Mention), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(Strings.BirthdayAdminRemoved(ctx.Guild.Id, user.Mention));
        }
    }

    /// <summary>
    ///     Deletes your birthday and every birthday setting stored for you.
    /// </summary>
    [SlashCommand("forget", "Delete your birthday and birthday settings")]
    public async Task BirthdayForget()
    {
        await Service.ClearUserBirthdayAsync(ctx.User, true);
        await ctx.Interaction.SendConfirmAsync(Strings.BirthdayForgotten(ctx.Guild.Id));
    }

    #endregion
}