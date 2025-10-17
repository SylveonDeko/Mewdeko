using System.Text;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.CustomVoice.Services;

namespace Mewdeko.Modules.CustomVoice;

/// <summary>
///     Commands for managing custom voice channels.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="settingsService">The settingsservice service.</param>
public class CustomVoice(IDataConnectionFactory dbFactory, GuildSettingsService settingsService)
    : MewdekoModuleBase<CustomVoiceService>
{
    /// <summary>
    ///     Shows interactive controls for managing your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceControls()
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner or has admin permissions
        var isOwner = customChannel.OwnerId == user.Id;
        var hasAdminRole = false;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            hasAdminRole = config.CustomVoiceAdminRoleId.HasValue &&
                           user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await ReplyErrorAsync(Strings.CustomVoiceControlsNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Get the actual Discord channel
        var channel = await Context.Guild.GetVoiceChannelAsync(customChannel.ChannelId);
        if (channel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceChannelNotFound(Context.Guild.Id));
            return;
        }

        // Get server config to check permissions
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        var users = await channel.GetUsersAsync().FlattenAsync();
        users = users.Where(x => x.VoiceChannel == channel);

        // Build the embed
        var embed = new EmbedBuilder()
            .WithTitle(Strings.CustomVoiceControlsTitle(Context.Guild.Id, channel.Name))
            .WithOkColor()
            .WithDescription(Strings.CustomVoiceControlsDesc(Context.Guild.Id))
            .AddField(Strings.CustomVoiceControlsOwner(Context.Guild.Id),
                MentionUtils.MentionUser(customChannel.OwnerId), true)
            .AddField(Strings.CustomVoiceControlsCreated(Context.Guild.Id),
                Strings.CustomVoiceControlsTimeAgo(Context.Guild.Id,
                    (DateTime.UtcNow - customChannel.CreatedAt).TotalHours.ToString("F1")), true)
            .AddField(Strings.CustomVoiceControlsUserLimit(Context.Guild.Id),
                channel.UserLimit == 0
                    ? Strings.CustomVoiceConfigUnlimited(Context.Guild.Id)
                    : channel.UserLimit.ToString(), true)
            .AddField(Strings.CustomVoiceControlsBitrate(Context.Guild.Id), $"{channel.Bitrate / 1000} kbps", true)
            .AddField(Strings.CustomVoiceControlsLocked(Context.Guild.Id),
                customChannel.IsLocked
                    ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                    : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
            .AddField(Strings.CustomVoiceControlsKeepAlive(Context.Guild.Id),
                customChannel.KeepAlive
                    ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                    : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
            .AddField(Strings.CustomVoiceControlsUsers(Context.Guild.Id),
                !users.Any()
                    ? Strings.CustomVoiceControlsNoUsers(Context.Guild.Id)
                    : string.Join(", ", users.Select(u => u.Mention)));

        // Create the components for quick actions
        var components = new ComponentBuilder();

        // Row 1: Basic controls
        components.WithButton(
            customId: $"voice:rename:{channel.Id}",
            label: Strings.CustomVoiceControlsRenameButton(Context.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowNameCustomization,
            row: 0
        );

        components.WithButton(
            customId: $"voice:limit:{channel.Id}",
            label: Strings.CustomVoiceControlsLimitButton(Context.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowUserLimitCustomization,
            row: 0
        );

        components.WithButton(
            customId: $"voice:bitrate:{channel.Id}",
            label: Strings.CustomVoiceControlsBitrateButton(Context.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowBitrateCustomization,
            row: 0
        );

        // Row 2: Lock/Keep Alive toggles
        components.WithButton(
            customId: $"voice:{(customChannel.IsLocked ? "unlock" : "lock")}:{channel.Id}",
            label: customChannel.IsLocked
                ? Strings.CustomVoiceControlsUnlockButton(Context.Guild.Id)
                : Strings.CustomVoiceControlsLockButton(Context.Guild.Id),
            style: customChannel.IsLocked ? ButtonStyle.Success : ButtonStyle.Danger,
            disabled: !guildConfig.AllowLocking,
            row: 1
        );

        components.WithButton(
            customId: $"voice:keepalive:{channel.Id}:{!customChannel.KeepAlive}",
            label: customChannel.KeepAlive
                ? Strings.CustomVoiceControlsDisableKeepAliveButton(Context.Guild.Id)
                : Strings.CustomVoiceControlsEnableKeepAliveButton(Context.Guild.Id),
            style: customChannel.KeepAlive ? ButtonStyle.Danger : ButtonStyle.Success,
            row: 1
        );

        components.WithButton(
            customId: $"voice:transfer:{channel.Id}",
            label: Strings.CustomVoiceControlsTransferButton(Context.Guild.Id),
            style: ButtonStyle.Secondary,
            row: 1
        );

        // Row 3: User management dropdown
        if (guildConfig.AllowUserManagement && (users.Count() > 1 || customChannel.IsLocked))
        {
            var userSelect = new SelectMenuBuilder()
                .WithPlaceholder(Strings.CustomVoiceControlsManageUsersPlaceholder(Context.Guild.Id))
                .WithCustomId($"voice:usermenu:{channel.Id}")
                .WithMinValues(1)
                .WithMaxValues(1);

            // Add options for all users except the channel owner
            foreach (var channelUser in users.Where(u => u.Id != customChannel.OwnerId))
            {
                userSelect.AddOption(
                    Strings.CustomVoiceControlsKickOption(Context.Guild.Id, channelUser.Username),
                    $"kick:{channelUser.Id}",
                    Strings.CustomVoiceControlsKickDesc(Context.Guild.Id),
                    new Emoji("👢")
                );
            }

            // Add options for all users in the guild
            foreach (var guildUser in await Context.Guild.GetUsersAsync())
            {
                // Skip users who are in the voice channel to avoid duplicate entries
                if (users.Any(u => u.Id == guildUser.Id) || guildUser.Id == Context.Client.CurrentUser.Id)
                    continue;

                // Only add a reasonable number of users to avoid hitting Discord's limits
                if (userSelect.Options.Count >= 20)
                    break;

                // Check if the user is allowed or denied
                var isAllowed = false;
                var isDenied = false;

                // Parse allowed/denied users from JSON
                if (!string.IsNullOrEmpty(customChannel.AllowedUsersJson))
                {
                    try
                    {
                        var allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson);
                        isAllowed = allowedUsers?.Contains(guildUser.Id) == true;
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(customChannel.DeniedUsersJson))
                {
                    try
                    {
                        var deniedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.DeniedUsersJson);
                        isDenied = deniedUsers?.Contains(guildUser.Id) == true;
                    }
                    catch { }
                }

                // Add allow/deny options
                if (isAllowed)
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsRemoveAllowOption(Context.Guild.Id, guildUser.Username),
                        $"unallow:{guildUser.Id}",
                        Strings.CustomVoiceControlsRemoveAllowDesc(Context.Guild.Id),
                        new Emoji("❌")
                    );
                }
                else if (isDenied)
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsRemoveDenyOption(Context.Guild.Id, guildUser.Username),
                        $"undeny:{guildUser.Id}",
                        Strings.CustomVoiceControlsRemoveDenyDesc(Context.Guild.Id),
                        new Emoji("✅")
                    );
                }
                else
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsAllowOption(Context.Guild.Id, guildUser.Username),
                        $"allow:{guildUser.Id}",
                        Strings.CustomVoiceControlsAllowDesc(Context.Guild.Id),
                        new Emoji("✅")
                    );

                    userSelect.AddOption(
                        Strings.CustomVoiceControlsDenyOption(Context.Guild.Id, guildUser.Username),
                        $"deny:{guildUser.Id}",
                        Strings.CustomVoiceControlsDenyDesc(Context.Guild.Id),
                        new Emoji("❌")
                    );
                }
            }

            // Only add the select menu if there are options
            if (userSelect.Options.Count > 0)
            {
                components.WithSelectMenu(userSelect, 2);
            }
        }

        await ReplyAsync(embed: embed.Build(), components: components.Build());
    }

    /// <summary>
    ///     Sets up a voice channel as a hub for creating custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetupVoiceHub()
    {
        // Create the join-to-create channel
        var channel = await Context.Guild.CreateVoiceChannelAsync(Strings.CustomVoiceHubDefaultName(Context.Guild.Id));
        var category =
            await Context.Guild.CreateCategoryAsync(Strings.CustomVoiceCategoryDefaultName(Context.Guild.Id));

        // Set permissions for the hub channel
        await channel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(
            connect: PermValue.Allow,
            speak: PermValue.Deny
        ));

        // Move the channel to the category
        await channel.ModifyAsync(props => props.CategoryId = category.Id);

        // Setup the hub in the service
        await Service.SetupHubAsync(Context.Guild.Id, channel.Id, category.Id);

        await ReplyConfirmAsync(Strings.CustomVoiceHubCreated(Context.Guild.Id, channel.Name));
    }

    /// <summary>
    ///     Sets a specific voice channel as the hub for creating custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetVoiceHub(IVoiceChannel channel, ICategoryChannel category = null)
    {
        // Setup the hub
        await Service.SetupHubAsync(Context.Guild.Id, channel.Id, category?.Id);

        if (category != null)
        {
            await ReplyConfirmAsync(
                Strings.CustomVoiceHubSetWithCategory(Context.Guild.Id, channel.Name, category.Name));
        }
        else
        {
            await ReplyConfirmAsync(Strings.CustomVoiceHubSet(Context.Guild.Id, channel.Name));
        }
    }

    /// <summary>
    ///     Configures the custom voice settings.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task VoiceConfig(string setting = null, string value = null)
    {
        var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);

        // If no parameters, show current configuration
        if (setting == null)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CustomVoiceConfigTitle(Context.Guild.Id))
                .WithOkColor()
                .AddField(Strings.CustomVoiceConfigHubChannel(Context.Guild.Id), $"<#{config.HubVoiceChannelId}>", true)
                .AddField(Strings.CustomVoiceConfigCategory(Context.Guild.Id),
                    config.ChannelCategoryId.HasValue
                        ? $"<#{config.ChannelCategoryId}>"
                        : Strings.CustomVoiceConfigNone(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigNameFormat(Context.Guild.Id), config.DefaultNameFormat, true)
                .AddField(Strings.CustomVoiceConfigUserLimit(Context.Guild.Id),
                    config.DefaultUserLimit == 0
                        ? Strings.CustomVoiceConfigUnlimited(Context.Guild.Id)
                        : config.DefaultUserLimit.ToString(), true)
                .AddField(Strings.CustomVoiceConfigBitrate(Context.Guild.Id), $"{config.DefaultBitrate} kbps", true)
                .AddField(Strings.CustomVoiceConfigDeleteEmpty(Context.Guild.Id),
                    config.DeleteWhenEmpty
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigEmptyTimeout(Context.Guild.Id),
                    Strings.CustomVoiceConfigMinutes(Context.Guild.Id, config.EmptyChannelTimeout), true)
                .AddField(Strings.CustomVoiceConfigMultipleChannels(Context.Guild.Id),
                    config.AllowMultipleChannels
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigNameCustomization(Context.Guild.Id),
                    config.AllowNameCustomization
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigLimitCustomization(Context.Guild.Id),
                    config.AllowUserLimitCustomization
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigBitrateCustomization(Context.Guild.Id),
                    config.AllowBitrateCustomization
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigLocking(Context.Guild.Id),
                    config.AllowLocking
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigUserManagement(Context.Guild.Id),
                    config.AllowUserManagement
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigMaxUserLimit(Context.Guild.Id),
                    config.MaxUserLimit == 0
                        ? Strings.CustomVoiceConfigNoMaximum(Context.Guild.Id)
                        : config.MaxUserLimit.ToString(), true)
                .AddField(Strings.CustomVoiceConfigMaxBitrate(Context.Guild.Id), $"{config.MaxBitrate} kbps", true)
                .AddField(Strings.CustomVoiceConfigPersistPreferences(Context.Guild.Id),
                    config.PersistUserPreferences
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigAutoPermission(Context.Guild.Id),
                    config.AutoPermission
                        ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                        : Strings.CustomVoiceConfigNo(Context.Guild.Id), true)
                .AddField(Strings.CustomVoiceConfigAdminRole(Context.Guild.Id),
                    config.CustomVoiceAdminRoleId.HasValue
                        ? $"<@&{config.CustomVoiceAdminRoleId}>"
                        : Strings.CustomVoiceConfigNone(Context.Guild.Id), true);

            await ReplyAsync(embed: embed.Build());
            return;
        }

        // If only setting provided, show current value
        if (value == null)
        {
            var currentValue = setting.ToLower() switch
            {
                "nameformat" => config.DefaultNameFormat,
                "userlimit" => config.DefaultUserLimit.ToString(),
                "bitrate" => config.DefaultBitrate.ToString(),
                "deleteempty" => config.DeleteWhenEmpty.ToString(),
                "emptytimeout" => config.EmptyChannelTimeout.ToString(),
                "multiplechannels" => config.AllowMultipleChannels.ToString(),
                "namechange" => config.AllowNameCustomization.ToString(),
                "limitchange" => config.AllowUserLimitCustomization.ToString(),
                "bitratechange" => config.AllowBitrateCustomization.ToString(),
                "locking" => config.AllowLocking.ToString(),
                "usermanagement" => config.AllowUserManagement.ToString(),
                "maxuserlimit" => config.MaxUserLimit.ToString(),
                "maxbitrate" => config.MaxBitrate.ToString(),
                "persistpreferences" => config.PersistUserPreferences.ToString(),
                "autopermission" => config.AutoPermission.ToString(),
                "adminrole" => config.CustomVoiceAdminRoleId?.ToString() ??
                               Strings.CustomVoiceConfigNone(Context.Guild.Id),
                _ => null
            };

            if (currentValue != null)
            {
                await ReplyConfirmAsync(Strings.CustomVoiceConfigCurrentValue(Context.Guild.Id, setting, currentValue));
            }
            else
            {
                var pref = await settingsService.GetPrefix(ctx.Guild);
                await ReplyErrorAsync(Strings.CustomVoiceConfigUnknownSetting(Context.Guild.Id, setting, pref));
            }

            return;
        }

        // Update the requested setting
        var updated = true;
        switch (setting.ToLower())
        {
            case "nameformat":
                config.DefaultNameFormat = value;
                break;

            case "userlimit":
                if (int.TryParse(value, out var userLimit) && userLimit >= 0)
                {
                    config.DefaultUserLimit = userLimit;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigUserLimitInvalid(Context.Guild.Id));
                    return;
                }

                break;

            case "bitrate":
                if (int.TryParse(value, out var bitrate) && bitrate > 0)
                {
                    config.DefaultBitrate = bitrate;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBitrateInvalid(Context.Guild.Id));
                    return;
                }

                break;

            case "deleteempty":
                if (bool.TryParse(value, out var deleteEmpty))
                {
                    config.DeleteWhenEmpty = deleteEmpty;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "emptytimeout":
                if (int.TryParse(value, out var emptyTimeout) && emptyTimeout >= 0)
                {
                    config.EmptyChannelTimeout = emptyTimeout;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigTimeoutInvalid(Context.Guild.Id));
                    return;
                }

                break;

            case "multiplechannels":
                if (bool.TryParse(value, out var multipleChannels))
                {
                    config.AllowMultipleChannels = multipleChannels;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "namechange":
                if (bool.TryParse(value, out var nameChange))
                {
                    config.AllowNameCustomization = nameChange;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "limitchange":
                if (bool.TryParse(value, out var limitChange))
                {
                    config.AllowUserLimitCustomization = limitChange;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "bitratechange":
                if (bool.TryParse(value, out var bitrateChange))
                {
                    config.AllowBitrateCustomization = bitrateChange;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "locking":
                if (bool.TryParse(value, out var locking))
                {
                    config.AllowLocking = locking;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "usermanagement":
                if (bool.TryParse(value, out var userManagement))
                {
                    config.AllowUserManagement = userManagement;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "maxuserlimit":
                if (int.TryParse(value, out var maxUserLimit) && maxUserLimit >= 0)
                {
                    config.MaxUserLimit = maxUserLimit;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigMaxUserLimitInvalid(Context.Guild.Id));
                    return;
                }

                break;

            case "maxbitrate":
                if (int.TryParse(value, out var maxBitrate) && maxBitrate > 0)
                {
                    // Validate against Discord's limits based on server boost level
                    var maxAllowed = Context.Guild.PremiumTier switch
                    {
                        PremiumTier.Tier3 => 384,
                        PremiumTier.Tier2 => 256,
                        PremiumTier.Tier1 => 128,
                        _ => 96
                    };

                    if (maxBitrate > maxAllowed)
                    {
                        await ReplyErrorAsync(
                            $"Max bitrate cannot exceed {maxAllowed} kbps for your server's boost level ({Context.Guild.PremiumTier}).");
                        return;
                    }

                    config.MaxBitrate = maxBitrate;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigMaxBitrateInvalid(Context.Guild.Id));
                    return;
                }

                break;

            case "persistpreferences":
                if (bool.TryParse(value, out var persistPreferences))
                {
                    config.PersistUserPreferences = persistPreferences;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "autopermission":
                if (bool.TryParse(value, out var autoPermission))
                {
                    config.AutoPermission = autoPermission;
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(Context.Guild.Id));
                    return;
                }

                break;

            case "adminrole":
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    config.CustomVoiceAdminRoleId = null;
                }
                else if (MentionUtils.TryParseRole(value, out var roleId))
                {
                    config.CustomVoiceAdminRoleId = roleId;
                }
                else
                {
                    var role = Context.Guild.Roles.FirstOrDefault(r =>
                        r.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (role != null)
                    {
                        config.CustomVoiceAdminRoleId = role.Id;
                    }
                    else
                    {
                        await ReplyErrorAsync(Strings.CustomVoiceConfigInvalidRole(Context.Guild.Id));
                        return;
                    }
                }

                break;

            default:
                updated = false;
                var pref = await settingsService.GetPrefix(ctx.Guild);
                await ReplyErrorAsync(Strings.CustomVoiceConfigUnknownSetting(Context.Guild.Id, setting, pref));
                break;
        }

        if (updated)
        {
            await Service.UpdateConfigAsync(config);
            await ReplyConfirmAsync(Strings.CustomVoiceConfigUpdated(Context.Guild.Id, setting, value));
        }
    }

    /// <summary>
    ///     Lists active custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceChannels()
    {
        var channels = await Service.GetActiveChannelsAsync(Context.Guild.Id);

        if (channels.Count == 0)
        {
            await ReplyConfirmAsync(Strings.CustomVoiceNoActiveChannels(Context.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CustomVoiceChannelsTitle(Context.Guild.Id))
            .WithOkColor()
            .WithDescription(Strings.CustomVoiceChannelsCount(Context.Guild.Id, channels.Count));

        var sb = new StringBuilder();

        foreach (var channel in channels)
        {
            // Get the Discord channel to check if it still exists
            var voiceChannel = await Context.Guild.GetVoiceChannelAsync(channel.ChannelId);
            if (voiceChannel == null)
                continue;

            var userCount = (await voiceChannel.GetUsersAsync().FlattenAsync()).Count();
            var ownerMention = MentionUtils.MentionUser(channel.OwnerId);

            sb.AppendLine($"**{voiceChannel.Name}** ({channel.ChannelId})");
            sb.AppendLine(Strings.CustomVoiceChannelOwner(Context.Guild.Id, ownerMention));
            sb.AppendLine(Strings.CustomVoiceChannelUsers(Context.Guild.Id, userCount));
            sb.AppendLine(Strings.CustomVoiceChannelLocked(Context.Guild.Id,
                channel.IsLocked
                    ? Strings.CustomVoiceConfigYes(Context.Guild.Id)
                    : Strings.CustomVoiceConfigNo(Context.Guild.Id)));
            sb.AppendLine(Strings.CustomVoiceChannelCreated(Context.Guild.Id,
                (DateTime.UtcNow - channel.CreatedAt).TotalHours.ToString("F1")));
            sb.AppendLine();
        }

        embed.WithDescription(sb.ToString());

        await ReplyAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Renames your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceRename([Remainder] string name)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if name customization is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowNameCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNameCustomizationDisabled(Context.Guild.Id));
            return;
        }

        // Update the channel name
        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, user.VoiceChannel.Id, name))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceRenamed(Context.Guild.Id, name));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceRenameError(Context.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the user limit for your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceLimit(int limit)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if limit customization is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowUserLimitCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitCustomizationDisabled(Context.Guild.Id));
            return;
        }

        // Validate the limit
        if (limit < 0)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitNegative(Context.Guild.Id));
            return;
        }

        // Update the user limit
        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, user.VoiceChannel.Id, userLimit: limit))
        {
            var limitText = limit == 0 ? Strings.CustomVoiceConfigUnlimited(Context.Guild.Id) : limit.ToString();
            await ReplyConfirmAsync(Strings.CustomVoiceLimitSet(Context.Guild.Id, limitText));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitError(Context.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the bitrate for your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceBitrate(int bitrate)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if bitrate customization is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowBitrateCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateCustomizationDisabled(Context.Guild.Id));
            return;
        }

        // Validate the bitrate
        if (bitrate <= 0)
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateNegative(Context.Guild.Id));
            return;
        }

        // Update the bitrate
        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, user.VoiceChannel.Id, bitrate: bitrate))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceBitrateSet(Context.Guild.Id, bitrate));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateError(Context.Guild.Id));
        }
    }

    /// <summary>
    ///     Locks or unlocks your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceLock(bool locked = true)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if locking is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowLocking)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLockingDisabled(Context.Guild.Id));
            return;
        }

        // Don't update if it's already in the desired state
        if (customChannel.IsLocked == locked)
        {
            var stateText = locked
                ? Strings.CustomVoiceAlreadyLocked(Context.Guild.Id)
                : Strings.CustomVoiceAlreadyUnlocked(Context.Guild.Id);

            await ReplyConfirmAsync(stateText);
            return;
        }

        // Update the lock status
        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, user.VoiceChannel.Id, isLocked: locked))
        {
            var actionText = locked
                ? Strings.CustomVoiceLocked(Context.Guild.Id)
                : Strings.CustomVoiceUnlocked(Context.Guild.Id);

            await ReplyConfirmAsync(actionText);
        }
        else
        {
            var errorText = locked
                ? Strings.CustomVoiceLockError(Context.Guild.Id)
                : Strings.CustomVoiceUnlockError(Context.Guild.Id);

            await ReplyErrorAsync(errorText);
        }
    }

    /// <summary>
    ///     Unlocks your custom voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceUnlock()
    {
        await VoiceLock(false);
    }

    /// <summary>
    ///     Allows a specific user to join your locked voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceAllow(IGuildUser target)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if user management is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowUserManagement)
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserManagementDisabled(Context.Guild.Id));
            return;
        }

        // Allow the user
        if (await Service.AllowUserAsync(Context.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceUserAllowed(Context.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserAllowError(Context.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Denies a specific user from joining your voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceDeny(IGuildUser target)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Check if user management is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.AllowUserManagement)
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserManagementDisabled(Context.Guild.Id));
            return;
        }

        // Can't deny the owner
        if (customChannel.OwnerId == target.Id)
        {
            await ReplyErrorAsync(Strings.CustomVoiceCantDenyOwner(Context.Guild.Id));
            return;
        }

        // Deny the user
        if (await Service.DenyUserAsync(Context.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceUserDenied(Context.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserDenyError(Context.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Claims ownership of a custom voice channel if the owner is not present.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceClaim()
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Already the owner?
        if (customChannel.OwnerId == user.Id)
        {
            await ReplyConfirmAsync(Strings.CustomVoiceAlreadyOwner(Context.Guild.Id));
            return;
        }

        // Check if original owner is in the channel
        var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(user.VoiceChannel.Id);
        var originalOwner = await Context.Guild.GetUserAsync(customChannel.OwnerId);

        if (originalOwner != null &&
            (await voiceChannel.GetUsersAsync().FlattenAsync()).Any(u => u.Id == originalOwner.Id))
        {
            await ReplyErrorAsync(Strings.CustomVoiceOwnerPresent(Context.Guild.Id));
            return;
        }

        // Transfer ownership
        if (await Service.TransferOwnershipAsync(Context.Guild.Id, user.VoiceChannel.Id, user.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceOwnershipClaimed(Context.Guild.Id));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceClaimError(Context.Guild.Id));
        }
    }

    /// <summary>
    ///     Transfers ownership of your custom voice channel to another user.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceTransfer(IGuildUser target)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Target needs to be in the channel
        var voiceChannel = await Context.Guild.GetVoiceChannelAsync(user.VoiceChannel.Id);
        if ((await voiceChannel.GetUsersAsync().FlattenAsync()).All(u => u.Id != target.Id))
        {
            await ReplyErrorAsync(Strings.CustomVoiceTransferUserNotPresent(Context.Guild.Id));
            return;
        }

        // Transfer ownership
        if (await Service.TransferOwnershipAsync(Context.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceTransferSuccess(Context.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceTransferError(Context.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Sets the channel to be kept alive even when empty.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoiceKeepAlive(bool keepAlive = true)
    {
        // Check if user is in a voice channel
        var user = Context.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(Context.Guild.Id));
            return;
        }

        // Check if the channel is a custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(Context.Guild.Id));
            return;
        }

        // Check if user is the owner
        if (customChannel.OwnerId != user.Id)
        {
            // Check if the user has admin role
            var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
            if (!config.CustomVoiceAdminRoleId.HasValue || !user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            {
                await ReplyErrorAsync(Strings.CustomVoiceNotOwner(Context.Guild.Id));
                return;
            }
        }

        // Don't update if it's already in the desired state
        if (customChannel.KeepAlive == keepAlive)
        {
            var stateText = keepAlive
                ? Strings.CustomVoiceAlreadyKeepAlive(Context.Guild.Id)
                : Strings.CustomVoiceAlreadyNotKeepAlive(Context.Guild.Id);

            await ReplyConfirmAsync(stateText);
            return;
        }

        // Update the keep-alive status
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        customChannel.KeepAlive = keepAlive;
        await dbContext.UpdateAsync(customChannel);
        var actionText = keepAlive
            ? Strings.CustomVoiceKeptAlive(Context.Guild.Id)
            : Strings.CustomVoiceNotKeptAlive(Context.Guild.Id);

        await ReplyConfirmAsync(actionText);
    }

    /// <summary>
    ///     Sets your voice channel preferences.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePreferences()
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CustomVoicePreferencesTitle(Context.Guild.Id))
            .WithOkColor()
            .WithDescription(Strings.CustomVoicePreferencesDesc(Context.Guild.Id));

        // Show current preferences if they exist
        if (prefs != null)
        {
            embed.AddField(
                Strings.CustomVoicePrefsNameFormat(Context.Guild.Id),
                prefs.NameFormat ?? Strings.CustomVoicePrefsDefault(Context.Guild.Id),
                true);

            embed.AddField(
                Strings.CustomVoicePrefsUserLimit(Context.Guild.Id),
                prefs.UserLimit?.ToString() ?? Strings.CustomVoicePrefsDefault(Context.Guild.Id),
                true);

            embed.AddField(
                Strings.CustomVoicePrefsBitrate(Context.Guild.Id),
                prefs.Bitrate.HasValue
                    ? Strings.CustomVoicePrefsBitrateValue(Context.Guild.Id, prefs.Bitrate.Value)
                    : Strings.CustomVoicePrefsDefault(Context.Guild.Id),
                true);

            embed.AddField(
                Strings.CustomVoicePrefsLocked(Context.Guild.Id),
                prefs.PreferLocked?.ToString() ?? Strings.CustomVoicePrefsDefault(Context.Guild.Id),
                true);

            embed.AddField(
                Strings.CustomVoicePrefsKeepAlive(Context.Guild.Id),
                prefs.KeepAlive?.ToString() ?? Strings.CustomVoicePrefsDefault(Context.Guild.Id),
                true);

            // Parse whitelisted/blacklisted users
            List<ulong> whitelist = null;
            List<ulong> blacklist = null;

            if (!string.IsNullOrEmpty(prefs.WhitelistJson))
            {
                try
                {
                    whitelist = JsonSerializer.Deserialize<List<ulong>>(prefs.WhitelistJson);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(prefs.BlacklistJson))
            {
                try
                {
                    blacklist = JsonSerializer.Deserialize<List<ulong>>(prefs.BlacklistJson);
                }
                catch { }
            }

            if (whitelist?.Count > 0)
            {
                var whitelistUsers = whitelist.Select(uid => MentionUtils.MentionUser(uid));
                embed.AddField(Strings.CustomVoicePrefsAllowedUsers(Context.Guild.Id),
                    string.Join(", ", whitelistUsers));
            }

            if (blacklist?.Count > 0)
            {
                var blacklistUsers = blacklist.Select(uid => MentionUtils.MentionUser(uid));
                embed.AddField(Strings.CustomVoicePrefsDeniedUsers(Context.Guild.Id),
                    string.Join(", ", blacklistUsers));
            }
        }
        else
        {
            embed.AddField(
                Strings.CustomVoicePrefsNoPrefs(Context.Guild.Id),
                Strings.CustomVoicePrefsNoPrefsDesc(Context.Guild.Id));
        }

        var pref = await settingsService.GetPrefix(ctx.Guild);

        // Add command usage information
        embed.AddField(Strings.CustomVoicePrefsCommands(Context.Guild.Id),
            Strings.CustomVoicePrefsCommandsList(Context.Guild.Id, pref));

        await ReplyAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Sets your preferred name format for custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsName([Remainder] string format = null)
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        // Get current preferences or create new ones
        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);
        if (prefs == null)
        {
            prefs = new UserVoicePreference
            {
                GuildId = Context.Guild.Id, UserId = Context.User.Id
            };
        }

        // If no format is provided, reset to default
        if (string.IsNullOrWhiteSpace(format))
        {
            prefs.NameFormat = null;
            await Service.SetUserPreferencesAsync(prefs);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsNameReset(Context.Guild.Id));
            return;
        }

        // Set the new format
        prefs.NameFormat = format;
        await Service.SetUserPreferencesAsync(prefs);
        await ReplyConfirmAsync(Strings.CustomVoicePrefsNameSet(Context.Guild.Id, format));
    }

    /// <summary>
    ///     Sets your preferred user limit for custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsLimit(int? limit = null)
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        // Get current preferences or create new ones
        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);
        if (prefs == null)
        {
            prefs = new UserVoicePreference
            {
                GuildId = Context.Guild.Id, UserId = Context.User.Id
            };
        }

        switch (limit)
        {
            // If no limit is provided, reset to default
            case null:
                prefs.UserLimit = null;
                await Service.SetUserPreferencesAsync(prefs);
                await ReplyConfirmAsync(Strings.CustomVoicePrefsLimitReset(Context.Guild.Id));
                return;
            // Validate the limit
            case < 0:
                await ReplyErrorAsync(Strings.CustomVoiceLimitNegative(Context.Guild.Id));
                return;
        }

        // Set the new limit
        prefs.UserLimit = limit;
        await Service.SetUserPreferencesAsync(prefs);

        var limitText = limit == 0 ? Strings.CustomVoiceConfigUnlimited(Context.Guild.Id) : limit.ToString();
        await ReplyConfirmAsync(Strings.CustomVoicePrefsLimitSet(Context.Guild.Id, limitText));
    }

    /// <summary>
    ///     Sets your preferred bitrate for custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsBitrate(int? bitrate = null)
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        // Get current preferences or create new ones
        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);
        if (prefs == null)
        {
            prefs = new UserVoicePreference
            {
                GuildId = Context.Guild.Id, UserId = Context.User.Id
            };
        }

        switch (bitrate)
        {
            // If no bitrate is provided, reset to default
            case null:
                prefs.Bitrate = null;
                await Service.SetUserPreferencesAsync(prefs);
                await ReplyConfirmAsync(Strings.CustomVoicePrefsBitrateReset(Context.Guild.Id));
                return;
            // Validate the bitrate
            case <= 0:
                await ReplyErrorAsync(Strings.CustomVoiceBitrateNegative(Context.Guild.Id));
                return;
        }

        // Set the new bitrate
        prefs.Bitrate = bitrate;
        await Service.SetUserPreferencesAsync(prefs);
        await ReplyConfirmAsync(Strings.CustomVoicePrefsBitrateSet(Context.Guild.Id, bitrate.Value));
    }

    /// <summary>
    ///     Sets your preferred lock status for custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsLock(bool? locked = null)
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        // Get current preferences or create new ones
        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);
        if (prefs == null)
        {
            prefs = new UserVoicePreference
            {
                GuildId = Context.Guild.Id, UserId = Context.User.Id
            };
        }

        // If no lock status is provided, reset to default
        if (locked == null)
        {
            prefs.PreferLocked = null;
            await Service.SetUserPreferencesAsync(prefs);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsLockReset(Context.Guild.Id));
            return;
        }

        // Set the new lock status
        prefs.PreferLocked = locked;
        await Service.SetUserPreferencesAsync(prefs);

        var lockStatus = locked.Value
            ? Strings.CustomVoicePrefsLocked(Context.Guild.Id)
            : Strings.CustomVoicePrefsUnlocked(Context.Guild.Id);
        await ReplyConfirmAsync(Strings.CustomVoicePrefsLockSet(Context.Guild.Id, lockStatus));
    }

    /// <summary>
    ///     Sets your preferred keep-alive status for custom voice channels.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsKeepAlive(bool? keepAlive = null)
    {
        var guildConfig = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        if (!guildConfig.PersistUserPreferences)
        {
            await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(Context.Guild.Id));
            return;
        }

        // Get current preferences or create new ones
        var prefs = await Service.GetUserPreferencesAsync(Context.Guild.Id, Context.User.Id);
        if (prefs == null)
        {
            prefs = new UserVoicePreference
            {
                GuildId = Context.Guild.Id, UserId = Context.User.Id
            };
        }

        // If no keep-alive status is provided, reset to default
        if (keepAlive == null)
        {
            prefs.KeepAlive = null;
            await Service.SetUserPreferencesAsync(prefs);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsKeepAliveReset(Context.Guild.Id));
            return;
        }

        // Set the new keep-alive status
        prefs.KeepAlive = keepAlive;
        await Service.SetUserPreferencesAsync(prefs);

        var keepAliveStatus = keepAlive.Value
            ? Strings.CustomVoicePrefsKeptAlive(Context.Guild.Id)
            : Strings.CustomVoicePrefsNotKeptAlive(Context.Guild.Id);
        await ReplyConfirmAsync(Strings.CustomVoicePrefsKeepAliveSet(Context.Guild.Id, keepAliveStatus));
    }

    /// <summary>
    ///     Resets all your voice channel preferences.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoicePrefsReset()
    {
        // Delete preferences from database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var prefs = await dbContext.UserVoicePreferences
            .FirstOrDefaultAsync(p => p.GuildId == Context.Guild.Id && p.UserId == Context.User.Id);

        if (prefs != null)
        {
            await dbContext.UserVoicePreferences.Select(x => prefs).DeleteAsync();
            await ReplyConfirmAsync(Strings.CustomVoicePrefsAllReset(Context.Guild.Id));
        }
        else
        {
            await ReplyConfirmAsync(Strings.CustomVoicePrefsNoneExist(Context.Guild.Id));
        }
    }
}