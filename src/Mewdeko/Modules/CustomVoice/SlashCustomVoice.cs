using System.Text;
using System.Text.Json;
using DataModel;
using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.CustomVoice.Services;

namespace Mewdeko.Modules.CustomVoice;

/// <summary>
///     Slash commands for managing custom voice channels.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
[Group("voice", "Custom voice channels")]
public class SlashCustomVoice(IDataConnectionFactory dbFactory)
    : MewdekoSlashModuleBase<CustomVoiceService>
{
    private async Task<(IGuildUser User, CustomVoiceChannel Channel)?> GetManagedChannelAsync(
        bool requireOwnerOrAdmin = true, string? notOwnerText = null)
    {
        var user = ctx.User as IGuildUser;
        if (user?.VoiceChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotInChannel(ctx.Guild.Id));
            return null;
        }

        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, user.VoiceChannel.Id);
        if (customChannel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNotCustomChannel(ctx.Guild.Id));
            return null;
        }

        if (!requireOwnerOrAdmin || customChannel.OwnerId == user.Id)
            return (user, customChannel);

        var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value))
            return (user, customChannel);

        await ReplyErrorAsync(notOwnerText ?? Strings.CustomVoiceNotOwner(ctx.Guild.Id));
        return null;
    }

    /// <summary>
    ///     Shows interactive controls for managing your custom voice channel.
    /// </summary>
    [SlashCommand("controls", "Show controls for your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceControls()
    {
        var managed = await GetManagedChannelAsync(true, Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
        if (managed is null)
            return;

        var (_, customChannel) = managed.Value;

        var channel = await ctx.Guild.GetVoiceChannelAsync(customChannel.ChannelId);
        if (channel == null)
        {
            await ReplyErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        await DeferAsync();

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        var users = await channel.GetUsersAsync().FlattenAsync();
        users = users.Where(x => x.VoiceChannel == channel);

        var userListValue = !users.Any()
            ? Strings.CustomVoiceControlsNoUsers(ctx.Guild.Id)
            : string.Join(", ", users.Select(u => u.Mention));

        if (string.IsNullOrWhiteSpace(userListValue))
            userListValue = "None";

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CustomVoiceControlsTitle(ctx.Guild.Id, channel.Name))
            .WithOkColor()
            .WithDescription(Strings.CustomVoiceControlsDesc(ctx.Guild.Id) ?? "Manage your voice channel")
            .AddField(Strings.CustomVoiceControlsOwner(ctx.Guild.Id) ?? "Owner",
                MentionUtils.MentionUser(customChannel.OwnerId), true)
            .AddField(Strings.CustomVoiceControlsCreated(ctx.Guild.Id) ?? "Created",
                Strings.CustomVoiceControlsTimeAgo(ctx.Guild.Id,
                    (DateTime.UtcNow - customChannel.CreatedAt).TotalHours.ToString("F1")) ?? "Unknown", true)
            .AddField(Strings.CustomVoiceControlsUserLimit(ctx.Guild.Id) ?? "User Limit",
                channel.UserLimit == 0
                    ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id) ?? "Unlimited"
                    : channel.UserLimit.ToString(), true)
            .AddField(Strings.CustomVoiceControlsBitrate(ctx.Guild.Id) ?? "Bitrate",
                $"{channel.Bitrate / 1000} kbps", true)
            .AddField(Strings.CustomVoiceControlsLocked(ctx.Guild.Id) ?? "Locked",
                customChannel.IsLocked
                    ? Strings.CustomVoiceConfigYes(ctx.Guild.Id) ?? "Yes"
                    : Strings.CustomVoiceConfigNo(ctx.Guild.Id) ?? "No", true)
            .AddField(Strings.CustomVoiceControlsKeepAlive(ctx.Guild.Id) ?? "Keep Alive",
                customChannel.KeepAlive
                    ? Strings.CustomVoiceConfigYes(ctx.Guild.Id) ?? "Yes"
                    : Strings.CustomVoiceConfigNo(ctx.Guild.Id) ?? "No", true)
            .AddField(Strings.CustomVoiceControlsUsers(ctx.Guild.Id) ?? "Users", userListValue);

        var components = new ComponentBuilder();

        components.WithButton(
            customId: $"voice:rename:{channel.Id}",
            label: Strings.CustomVoiceControlsRenameButton(ctx.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowNameCustomization,
            row: 0
        );

        components.WithButton(
            customId: $"voice:limit:{channel.Id}",
            label: Strings.CustomVoiceControlsLimitButton(ctx.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowUserLimitCustomization,
            row: 0
        );

        components.WithButton(
            customId: $"voice:bitrate:{channel.Id}",
            label: Strings.CustomVoiceControlsBitrateButton(ctx.Guild.Id),
            style: ButtonStyle.Primary,
            disabled: !guildConfig.AllowBitrateCustomization,
            row: 0
        );

        components.WithButton(
            customId: $"voice:{(customChannel.IsLocked ? "unlock" : "lock")}:{channel.Id}",
            label: customChannel.IsLocked
                ? Strings.CustomVoiceControlsUnlockButton(ctx.Guild.Id)
                : Strings.CustomVoiceControlsLockButton(ctx.Guild.Id),
            style: customChannel.IsLocked ? ButtonStyle.Success : ButtonStyle.Danger,
            disabled: !guildConfig.AllowLocking,
            row: 1
        );

        components.WithButton(
            customId: $"voice:keepalive:{channel.Id}:{!customChannel.KeepAlive}",
            label: customChannel.KeepAlive
                ? Strings.CustomVoiceControlsDisableKeepAliveButton(ctx.Guild.Id)
                : Strings.CustomVoiceControlsEnableKeepAliveButton(ctx.Guild.Id),
            style: customChannel.KeepAlive ? ButtonStyle.Danger : ButtonStyle.Success,
            row: 1
        );

        components.WithButton(
            customId: $"voice:transfer:{channel.Id}",
            label: Strings.CustomVoiceControlsTransferButton(ctx.Guild.Id),
            style: ButtonStyle.Secondary,
            row: 1
        );

        if (guildConfig.AllowUserManagement && (users.Count() > 1 || customChannel.IsLocked))
        {
            var userSelect = new SelectMenuBuilder()
                .WithPlaceholder(Strings.CustomVoiceControlsManageUsersPlaceholder(ctx.Guild.Id))
                .WithCustomId($"voice:usermenu:{channel.Id}")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var channelUser in users.Where(u => u.Id != customChannel.OwnerId))
            {
                userSelect.AddOption(
                    Strings.CustomVoiceControlsKickOption(ctx.Guild.Id, channelUser.Username),
                    $"kick:{channelUser.Id}",
                    Strings.CustomVoiceControlsKickDesc(ctx.Guild.Id)
                );
            }

            foreach (var guildUser in await ctx.Guild.GetUsersAsync())
            {
                if (users.Any(u => u.Id == guildUser.Id) || guildUser.Id == ctx.Client.CurrentUser.Id)
                    continue;

                if (userSelect.Options.Count >= 20)
                    break;

                var isAllowed = false;
                var isDenied = false;

                if (!string.IsNullOrEmpty(customChannel.AllowedUsersJson))
                {
                    try
                    {
                        var allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson);
                        isAllowed = allowedUsers?.Contains(guildUser.Id) == true;
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(customChannel.DeniedUsersJson))
                {
                    try
                    {
                        var deniedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.DeniedUsersJson);
                        isDenied = deniedUsers?.Contains(guildUser.Id) == true;
                    }
                    catch
                    {
                    }
                }

                if (isAllowed)
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsRemoveAllowOption(ctx.Guild.Id, guildUser.Username),
                        $"unallow:{guildUser.Id}",
                        Strings.CustomVoiceControlsRemoveAllowDesc(ctx.Guild.Id)
                    );
                }
                else if (isDenied)
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsRemoveDenyOption(ctx.Guild.Id, guildUser.Username),
                        $"undeny:{guildUser.Id}",
                        Strings.CustomVoiceControlsRemoveDenyDesc(ctx.Guild.Id)
                    );
                }
                else
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsAllowOption(ctx.Guild.Id, guildUser.Username),
                        $"allow:{guildUser.Id}",
                        Strings.CustomVoiceControlsAllowDesc(ctx.Guild.Id)
                    );

                    userSelect.AddOption(
                        Strings.CustomVoiceControlsDenyOption(ctx.Guild.Id, guildUser.Username),
                        $"deny:{guildUser.Id}",
                        Strings.CustomVoiceControlsDenyDesc(ctx.Guild.Id)
                    );
                }
            }

            if (userSelect.Options.Count > 0)
            {
                components.WithSelectMenu(userSelect, 2);
            }
        }

        await ctx.Interaction.FollowupAsync(embed: embed.Build(), components: components.Build());
    }

    /// <summary>
    ///     Lists active custom voice channels.
    /// </summary>
    [SlashCommand("channels", "List active custom voice channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceChannels()
    {
        await DeferAsync();
        var channels = await Service.GetActiveChannelsAsync(ctx.Guild.Id);

        if (channels.Count == 0)
        {
            await ReplyConfirmAsync(Strings.CustomVoiceNoActiveChannels(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CustomVoiceChannelsTitle(ctx.Guild.Id))
            .WithOkColor()
            .WithDescription(Strings.CustomVoiceChannelsCount(ctx.Guild.Id, channels.Count));

        var sb = new StringBuilder();

        foreach (var channel in channels)
        {
            var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(channel.ChannelId);
            if (voiceChannel == null)
                continue;

            var userCount = (await voiceChannel.GetUsersAsync().FlattenAsync()).Count();
            var ownerMention = MentionUtils.MentionUser(channel.OwnerId);

            sb.AppendLine($"**{voiceChannel.Name}** ({channel.ChannelId})");
            sb.AppendLine(Strings.CustomVoiceChannelOwner(ctx.Guild.Id, ownerMention));
            sb.AppendLine(Strings.CustomVoiceChannelUsers(ctx.Guild.Id, userCount));
            sb.AppendLine(Strings.CustomVoiceChannelLocked(ctx.Guild.Id,
                channel.IsLocked
                    ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                    : Strings.CustomVoiceConfigNo(ctx.Guild.Id)));
            sb.AppendLine(Strings.CustomVoiceChannelCreated(ctx.Guild.Id,
                (DateTime.UtcNow - channel.CreatedAt).TotalHours.ToString("F1")));
            sb.AppendLine();
        }

        embed.WithDescription(sb.ToString());

        await ctx.Interaction.FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Renames your custom voice channel.
    /// </summary>
    /// <param name="name">The new channel name.</param>
    [SlashCommand("rename", "Rename your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceRename([Summary("name", "The new channel name")] string name)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, _) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowNameCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceNameCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, user.VoiceChannel.Id, name))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceRenamed(ctx.Guild.Id, name));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceRenameError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the user limit for your custom voice channel.
    /// </summary>
    /// <param name="limit">The user limit, 0 for unlimited.</param>
    [SlashCommand("limit", "Set the user limit of your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceLimit([Summary("limit", "User limit, 0 for unlimited")] int limit)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, _) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowUserLimitCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (limit < 0)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitNegative(ctx.Guild.Id));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, user.VoiceChannel.Id, userLimit: limit))
        {
            var limitText = limit == 0 ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id) : limit.ToString();
            await ReplyConfirmAsync(Strings.CustomVoiceLimitSet(ctx.Guild.Id, limitText));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceLimitError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the bitrate for your custom voice channel.
    /// </summary>
    /// <param name="bitrate">The bitrate in kbps.</param>
    [SlashCommand("bitrate", "Set the bitrate of your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceBitrate([Summary("bitrate", "Bitrate in kbps")] int bitrate)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, _) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowBitrateCustomization)
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (bitrate <= 0)
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateNegative(ctx.Guild.Id));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, user.VoiceChannel.Id, bitrate: bitrate))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceBitrateSet(ctx.Guild.Id, bitrate));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceBitrateError(ctx.Guild.Id));
        }
    }

    private async Task SetLockedAsync(bool locked)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, customChannel) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowLocking)
        {
            await ReplyErrorAsync(Strings.CustomVoiceLockingDisabled(ctx.Guild.Id));
            return;
        }

        if (customChannel.IsLocked == locked)
        {
            var stateText = locked
                ? Strings.CustomVoiceAlreadyLocked(ctx.Guild.Id)
                : Strings.CustomVoiceAlreadyUnlocked(ctx.Guild.Id);

            await ReplyConfirmAsync(stateText);
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, user.VoiceChannel.Id, isLocked: locked))
        {
            var actionText = locked
                ? Strings.CustomVoiceLocked(ctx.Guild.Id)
                : Strings.CustomVoiceUnlocked(ctx.Guild.Id);

            await ReplyConfirmAsync(actionText);
        }
        else
        {
            var errorText = locked
                ? Strings.CustomVoiceLockError(ctx.Guild.Id)
                : Strings.CustomVoiceUnlockError(ctx.Guild.Id);

            await ReplyErrorAsync(errorText);
        }
    }

    /// <summary>
    ///     Locks your custom voice channel.
    /// </summary>
    [SlashCommand("lock", "Lock your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task VoiceLock()
    {
        return SetLockedAsync(true);
    }

    /// <summary>
    ///     Unlocks your custom voice channel.
    /// </summary>
    [SlashCommand("unlock", "Unlock your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task VoiceUnlock()
    {
        return SetLockedAsync(false);
    }

    /// <summary>
    ///     Allows a specific user to join your locked voice channel.
    /// </summary>
    /// <param name="target">The user to allow.</param>
    [SlashCommand("allow", "Allow a user to join your voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceAllow([Summary("user", "The user to allow")] IGuildUser target)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, _) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowUserManagement)
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserManagementDisabled(ctx.Guild.Id));
            return;
        }

        if (await Service.AllowUserAsync(ctx.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceUserAllowed(ctx.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserAllowError(ctx.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Denies a specific user from joining your voice channel.
    /// </summary>
    /// <param name="target">The user to deny.</param>
    [SlashCommand("deny", "Deny a user from joining your voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceDeny([Summary("user", "The user to deny")] IGuildUser target)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, customChannel) = managed.Value;

        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowUserManagement)
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserManagementDisabled(ctx.Guild.Id));
            return;
        }

        if (customChannel.OwnerId == target.Id)
        {
            await ReplyErrorAsync(Strings.CustomVoiceCantDenyOwner(ctx.Guild.Id));
            return;
        }

        if (await Service.DenyUserAsync(ctx.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceUserDenied(ctx.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceUserDenyError(ctx.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Claims ownership of a custom voice channel if the owner is not present.
    /// </summary>
    [SlashCommand("claim", "Claim a custom voice channel whose owner left")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceClaim()
    {
        var managed = await GetManagedChannelAsync(false);
        if (managed is null)
            return;

        var (user, customChannel) = managed.Value;

        if (customChannel.OwnerId == user.Id)
        {
            await ReplyConfirmAsync(Strings.CustomVoiceAlreadyOwner(ctx.Guild.Id));
            return;
        }

        var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(user.VoiceChannel.Id);
        var originalOwner = await ctx.Guild.GetUserAsync(customChannel.OwnerId);

        if (originalOwner != null &&
            (await voiceChannel.GetUsersAsync().FlattenAsync()).Any(u => u.Id == originalOwner.Id))
        {
            await ReplyErrorAsync(Strings.CustomVoiceOwnerPresent(ctx.Guild.Id));
            return;
        }

        if (await Service.TransferOwnershipAsync(ctx.Guild.Id, user.VoiceChannel.Id, user.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceOwnershipClaimed(ctx.Guild.Id));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceClaimError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Transfers ownership of your custom voice channel to another user.
    /// </summary>
    /// <param name="target">The user to transfer ownership to.</param>
    [SlashCommand("transfer", "Transfer ownership of your custom voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceTransfer([Summary("user", "The new owner")] IGuildUser target)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (user, _) = managed.Value;

        var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(user.VoiceChannel.Id);
        if ((await voiceChannel.GetUsersAsync().FlattenAsync()).All(u => u.Id != target.Id))
        {
            await ReplyErrorAsync(Strings.CustomVoiceTransferUserNotPresent(ctx.Guild.Id));
            return;
        }

        if (await Service.TransferOwnershipAsync(ctx.Guild.Id, user.VoiceChannel.Id, target.Id))
        {
            await ReplyConfirmAsync(Strings.CustomVoiceTransferSuccess(ctx.Guild.Id, target.Mention));
        }
        else
        {
            await ReplyErrorAsync(Strings.CustomVoiceTransferError(ctx.Guild.Id, target.Mention));
        }
    }

    /// <summary>
    ///     Sets whether the channel is kept alive even when empty.
    /// </summary>
    /// <param name="keepAlive">Whether to keep the channel alive when empty.</param>
    [SlashCommand("keep-alive", "Keep your custom voice channel alive when empty")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task VoiceKeepAlive([Summary("enabled", "Keep the channel alive when empty")] bool keepAlive = true)
    {
        var managed = await GetManagedChannelAsync();
        if (managed is null)
            return;

        var (_, customChannel) = managed.Value;

        if (customChannel.KeepAlive == keepAlive)
        {
            var stateText = keepAlive
                ? Strings.CustomVoiceAlreadyKeepAlive(ctx.Guild.Id)
                : Strings.CustomVoiceAlreadyNotKeepAlive(ctx.Guild.Id);

            await ReplyConfirmAsync(stateText);
            return;
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        customChannel.KeepAlive = keepAlive;
        await dbContext.UpdateAsync(customChannel);
        var actionText = keepAlive
            ? Strings.CustomVoiceKeptAlive(ctx.Guild.Id)
            : Strings.CustomVoiceNotKeptAlive(ctx.Guild.Id);

        await ReplyConfirmAsync(actionText);
    }

    /// <summary>
    ///     Administration commands for the custom voice system.
    /// </summary>
    [Group("admin", "Configure the custom voice system")]
    public class CustomVoiceAdmin : MewdekoSlashSubmodule<CustomVoiceService>
    {
        /// <summary>
        ///     Sets up a new voice channel and category as a hub for creating custom voice channels.
        /// </summary>
        [SlashCommand("setup-hub", "Create a join-to-create hub channel and category")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task SetupVoiceHub()
        {
            await DeferAsync();

            var channel = await ctx.Guild.CreateVoiceChannelAsync(Strings.CustomVoiceHubDefaultName(ctx.Guild.Id));
            var category =
                await ctx.Guild.CreateCategoryAsync(Strings.CustomVoiceCategoryDefaultName(ctx.Guild.Id));

            await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole, new OverwritePermissions(
                connect: PermValue.Allow,
                speak: PermValue.Deny
            ));

            await channel.ModifyAsync(props => props.CategoryId = category.Id);

            await Service.SetupHubAsync(ctx.Guild.Id, channel.Id, category.Id);

            await ReplyConfirmAsync(Strings.CustomVoiceHubCreated(ctx.Guild.Id, channel.Name));
        }

        /// <summary>
        ///     Sets a specific voice channel as the hub for creating custom voice channels.
        /// </summary>
        /// <param name="channel">The hub voice channel.</param>
        /// <param name="category">The category to create custom channels in.</param>
        [SlashCommand("set-hub", "Use an existing voice channel as the join-to-create hub")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task SetVoiceHub(
            [Summary("channel", "The hub voice channel")]
            IVoiceChannel channel,
            [Summary("category", "Category for created channels")]
            ICategoryChannel? category = null)
        {
            await Service.SetupHubAsync(ctx.Guild.Id, channel.Id, category?.Id);

            if (category != null)
            {
                await ReplyConfirmAsync(
                    Strings.CustomVoiceHubSetWithCategory(ctx.Guild.Id, channel.Name, category.Name));
            }
            else
            {
                await ReplyConfirmAsync(Strings.CustomVoiceHubSet(ctx.Guild.Id, channel.Name));
            }
        }

        /// <summary>
        ///     Views or configures the custom voice settings.
        /// </summary>
        /// <param name="setting">The setting to view or change. Leave empty to show everything.</param>
        /// <param name="value">The new value. Leave empty to show the current value.</param>
        [SlashCommand("config", "View or change custom voice settings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task VoiceConfig(
            [Summary("setting", "The setting to view or change")]
            [Autocomplete(typeof(VoiceConfigSettingAutocompleter))]
            string? setting = null,
            [Summary("value", "The new value")] string? value = null)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);

            if (setting == null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(Strings.CustomVoiceConfigTitle(ctx.Guild.Id))
                    .WithOkColor()
                    .AddField(Strings.CustomVoiceConfigHubChannel(ctx.Guild.Id), $"<#{config.HubVoiceChannelId}>",
                        true)
                    .AddField(Strings.CustomVoiceConfigCategory(ctx.Guild.Id),
                        config.ChannelCategoryId.HasValue
                            ? $"<#{config.ChannelCategoryId}>"
                            : Strings.CustomVoiceConfigNone(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigNameFormat(ctx.Guild.Id), config.DefaultNameFormat, true)
                    .AddField(Strings.CustomVoiceConfigUserLimit(ctx.Guild.Id),
                        config.DefaultUserLimit == 0
                            ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id)
                            : config.DefaultUserLimit.ToString(), true)
                    .AddField(Strings.CustomVoiceConfigBitrate(ctx.Guild.Id), $"{config.DefaultBitrate} kbps", true)
                    .AddField(Strings.CustomVoiceConfigDeleteEmpty(ctx.Guild.Id),
                        config.DeleteWhenEmpty
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigEmptyTimeout(ctx.Guild.Id),
                        Strings.CustomVoiceConfigMinutes(ctx.Guild.Id, config.EmptyChannelTimeout), true)
                    .AddField(Strings.CustomVoiceConfigMultipleChannels(ctx.Guild.Id),
                        config.AllowMultipleChannels
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigNameCustomization(ctx.Guild.Id),
                        config.AllowNameCustomization
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigLimitCustomization(ctx.Guild.Id),
                        config.AllowUserLimitCustomization
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigBitrateCustomization(ctx.Guild.Id),
                        config.AllowBitrateCustomization
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigLocking(ctx.Guild.Id),
                        config.AllowLocking
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigUserManagement(ctx.Guild.Id),
                        config.AllowUserManagement
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigMaxUserLimit(ctx.Guild.Id),
                        config.MaxUserLimit == 0
                            ? Strings.CustomVoiceConfigNoMaximum(ctx.Guild.Id)
                            : config.MaxUserLimit.ToString(), true)
                    .AddField(Strings.CustomVoiceConfigMaxBitrate(ctx.Guild.Id), $"{config.MaxBitrate} kbps", true)
                    .AddField(Strings.CustomVoiceConfigPersistPreferences(ctx.Guild.Id),
                        config.PersistUserPreferences
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigAutoPermission(ctx.Guild.Id),
                        config.AutoPermission
                            ? Strings.CustomVoiceConfigYes(ctx.Guild.Id)
                            : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                    .AddField(Strings.CustomVoiceConfigAdminRole(ctx.Guild.Id),
                        config.CustomVoiceAdminRoleId.HasValue
                            ? $"<@&{config.CustomVoiceAdminRoleId}>"
                            : Strings.CustomVoiceConfigNone(ctx.Guild.Id), true);

                await ctx.Interaction.RespondAsync(embed: embed.Build());
                return;
            }

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
                                   Strings.CustomVoiceConfigNone(ctx.Guild.Id),
                    _ => null
                };

                if (currentValue != null)
                {
                    await ReplyConfirmAsync(Strings.CustomVoiceConfigCurrentValue(ctx.Guild.Id, setting,
                        currentValue));
                }
                else
                {
                    await ReplyErrorAsync(Strings.CustomVoiceConfigUnknownSetting(ctx.Guild.Id, setting, "/"));
                }

                return;
            }

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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigUserLimitInvalid(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBitrateInvalid(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigTimeoutInvalid(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigMaxUserLimitInvalid(ctx.Guild.Id));
                        return;
                    }

                    break;

                case "maxbitrate":
                    if (int.TryParse(value, out var maxBitrate) && maxBitrate > 0)
                    {
                        var maxAllowed = ctx.Guild.PremiumTier switch
                        {
                            PremiumTier.Tier3 => 384,
                            PremiumTier.Tier2 => 256,
                            PremiumTier.Tier1 => 128,
                            _ => 96
                        };

                        if (maxBitrate > maxAllowed)
                        {
                            await ReplyErrorAsync(
                                $"Max bitrate cannot exceed {maxAllowed} kbps for your server's boost level ({ctx.Guild.PremiumTier}).");
                            return;
                        }

                        config.MaxBitrate = maxBitrate;
                    }
                    else
                    {
                        await ReplyErrorAsync(Strings.CustomVoiceConfigMaxBitrateInvalid(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
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
                        await ReplyErrorAsync(Strings.CustomVoiceConfigBooleanRequired(ctx.Guild.Id));
                        return;
                    }

                    break;

                case "adminrole":
                    if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        config.CustomVoiceAdminRoleId = null;
                    }
                    else if (MentionUtils.TryParseRole(value, out var roleId) || ulong.TryParse(value, out roleId))
                    {
                        config.CustomVoiceAdminRoleId = roleId;
                    }
                    else
                    {
                        var role = ctx.Guild.Roles.FirstOrDefault(r =>
                            r.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (role != null)
                        {
                            config.CustomVoiceAdminRoleId = role.Id;
                        }
                        else
                        {
                            await ReplyErrorAsync(Strings.CustomVoiceConfigInvalidRole(ctx.Guild.Id));
                            return;
                        }
                    }

                    break;

                default:
                    updated = false;
                    await ReplyErrorAsync(Strings.CustomVoiceConfigUnknownSetting(ctx.Guild.Id, setting, "/"));
                    break;
            }

            if (updated)
            {
                await Service.UpdateConfigAsync(config);
                await ReplyConfirmAsync(Strings.CustomVoiceConfigUpdated(ctx.Guild.Id, setting, value));
            }
        }
    }

    /// <summary>
    ///     Personal preference commands for custom voice channels.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    [Group("prefs", "Your custom voice channel preferences")]
    public class CustomVoicePrefs(IDataConnectionFactory dbFactory) : MewdekoSlashSubmodule<CustomVoiceService>
    {
        private async Task<UserVoicePreference?> GetPrefsOrErrorAsync()
        {
            var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            if (!guildConfig.PersistUserPreferences)
            {
                await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(ctx.Guild.Id));
                return null;
            }

            return await Service.GetUserPreferencesAsync(ctx.Guild.Id, ctx.User.Id) ?? new UserVoicePreference
            {
                GuildId = ctx.Guild.Id, UserId = ctx.User.Id
            };
        }

        /// <summary>
        ///     Shows your voice channel preferences.
        /// </summary>
        [SlashCommand("view", "Show your voice channel preferences")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePreferences()
        {
            var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            if (!guildConfig.PersistUserPreferences)
            {
                await ReplyErrorAsync(Strings.CustomVoicePreferencesDisabled(ctx.Guild.Id));
                return;
            }

            var prefs = await Service.GetUserPreferencesAsync(ctx.Guild.Id, ctx.User.Id);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.CustomVoicePreferencesTitle(ctx.Guild.Id))
                .WithOkColor()
                .WithDescription(Strings.CustomVoicePreferencesDesc(ctx.Guild.Id));

            if (prefs != null)
            {
                embed.AddField(
                    Strings.CustomVoicePrefsNameFormat(ctx.Guild.Id),
                    prefs.NameFormat ?? Strings.CustomVoicePrefsDefault(ctx.Guild.Id),
                    true);

                embed.AddField(
                    Strings.CustomVoicePrefsUserLimit(ctx.Guild.Id),
                    prefs.UserLimit?.ToString() ?? Strings.CustomVoicePrefsDefault(ctx.Guild.Id),
                    true);

                embed.AddField(
                    Strings.CustomVoicePrefsBitrate(ctx.Guild.Id),
                    prefs.Bitrate.HasValue
                        ? Strings.CustomVoicePrefsBitrateValue(ctx.Guild.Id, prefs.Bitrate.Value)
                        : Strings.CustomVoicePrefsDefault(ctx.Guild.Id),
                    true);

                embed.AddField(
                    Strings.CustomVoicePrefsLocked(ctx.Guild.Id),
                    prefs.PreferLocked?.ToString() ?? Strings.CustomVoicePrefsDefault(ctx.Guild.Id),
                    true);

                embed.AddField(
                    Strings.CustomVoicePrefsKeepAlive(ctx.Guild.Id),
                    prefs.KeepAlive?.ToString() ?? Strings.CustomVoicePrefsDefault(ctx.Guild.Id),
                    true);

                List<ulong>? whitelist = null;
                List<ulong>? blacklist = null;

                if (!string.IsNullOrEmpty(prefs.WhitelistJson))
                {
                    try
                    {
                        whitelist = JsonSerializer.Deserialize<List<ulong>>(prefs.WhitelistJson);
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(prefs.BlacklistJson))
                {
                    try
                    {
                        blacklist = JsonSerializer.Deserialize<List<ulong>>(prefs.BlacklistJson);
                    }
                    catch
                    {
                    }
                }

                if (whitelist?.Count > 0)
                {
                    var whitelistUsers = whitelist.Select(uid => MentionUtils.MentionUser(uid));
                    embed.AddField(Strings.CustomVoicePrefsAllowedUsers(ctx.Guild.Id),
                        string.Join(", ", whitelistUsers));
                }

                if (blacklist?.Count > 0)
                {
                    var blacklistUsers = blacklist.Select(uid => MentionUtils.MentionUser(uid));
                    embed.AddField(Strings.CustomVoicePrefsDeniedUsers(ctx.Guild.Id),
                        string.Join(", ", blacklistUsers));
                }
            }
            else
            {
                embed.AddField(
                    Strings.CustomVoicePrefsNoPrefs(ctx.Guild.Id),
                    Strings.CustomVoicePrefsNoPrefsDesc(ctx.Guild.Id));
            }

            embed.AddField(Strings.CustomVoicePrefsCommands(ctx.Guild.Id),
                Strings.CustomVoicePrefsCommandsList(ctx.Guild.Id, "/"));

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Sets your preferred name format for custom voice channels. Leave empty to reset.
        /// </summary>
        /// <param name="format">The name format, or empty to reset.</param>
        [SlashCommand("name", "Set your preferred channel name format")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsName([Summary("format", "Name format, leave empty to reset")] string? format = null)
        {
            var prefs = await GetPrefsOrErrorAsync();
            if (prefs is null)
                return;

            if (string.IsNullOrWhiteSpace(format))
            {
                prefs.NameFormat = null;
                await Service.SetUserPreferencesAsync(prefs);
                await ReplyConfirmAsync(Strings.CustomVoicePrefsNameReset(ctx.Guild.Id));
                return;
            }

            prefs.NameFormat = format;
            await Service.SetUserPreferencesAsync(prefs);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsNameSet(ctx.Guild.Id, format));
        }

        /// <summary>
        ///     Sets your preferred user limit for custom voice channels. Leave empty to reset.
        /// </summary>
        /// <param name="limit">The user limit, or empty to reset.</param>
        [SlashCommand("limit", "Set your preferred user limit")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsLimit([Summary("limit", "User limit, leave empty to reset")] int? limit = null)
        {
            var prefs = await GetPrefsOrErrorAsync();
            if (prefs is null)
                return;

            switch (limit)
            {
                case null:
                    prefs.UserLimit = null;
                    await Service.SetUserPreferencesAsync(prefs);
                    await ReplyConfirmAsync(Strings.CustomVoicePrefsLimitReset(ctx.Guild.Id));
                    return;
                case < 0:
                    await ReplyErrorAsync(Strings.CustomVoiceLimitNegative(ctx.Guild.Id));
                    return;
            }

            prefs.UserLimit = limit;
            await Service.SetUserPreferencesAsync(prefs);

            var limitText = limit == 0 ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id) : limit.ToString();
            await ReplyConfirmAsync(Strings.CustomVoicePrefsLimitSet(ctx.Guild.Id, limitText));
        }

        /// <summary>
        ///     Sets your preferred bitrate for custom voice channels. Leave empty to reset.
        /// </summary>
        /// <param name="bitrate">The bitrate in kbps, or empty to reset.</param>
        [SlashCommand("bitrate", "Set your preferred bitrate")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsBitrate(
            [Summary("bitrate", "Bitrate in kbps, leave empty to reset")]
            int? bitrate = null)
        {
            var prefs = await GetPrefsOrErrorAsync();
            if (prefs is null)
                return;

            switch (bitrate)
            {
                case null:
                    prefs.Bitrate = null;
                    await Service.SetUserPreferencesAsync(prefs);
                    await ReplyConfirmAsync(Strings.CustomVoicePrefsBitrateReset(ctx.Guild.Id));
                    return;
                case <= 0:
                    await ReplyErrorAsync(Strings.CustomVoiceBitrateNegative(ctx.Guild.Id));
                    return;
            }

            prefs.Bitrate = bitrate;
            await Service.SetUserPreferencesAsync(prefs);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsBitrateSet(ctx.Guild.Id, bitrate.Value));
        }

        /// <summary>
        ///     Sets your preferred lock status for custom voice channels. Leave empty to reset.
        /// </summary>
        /// <param name="locked">Whether channels start locked, or empty to reset.</param>
        [SlashCommand("lock", "Set whether your channels start locked")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsLock([Summary("locked", "Start locked, leave empty to reset")] bool? locked = null)
        {
            var prefs = await GetPrefsOrErrorAsync();
            if (prefs is null)
                return;

            if (locked == null)
            {
                prefs.PreferLocked = null;
                await Service.SetUserPreferencesAsync(prefs);
                await ReplyConfirmAsync(Strings.CustomVoicePrefsLockReset(ctx.Guild.Id));
                return;
            }

            prefs.PreferLocked = locked;
            await Service.SetUserPreferencesAsync(prefs);

            var lockStatus = locked.Value
                ? Strings.CustomVoicePrefsLocked(ctx.Guild.Id)
                : Strings.CustomVoicePrefsUnlocked(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsLockSet(ctx.Guild.Id, lockStatus));
        }

        /// <summary>
        ///     Sets your preferred keep-alive status for custom voice channels. Leave empty to reset.
        /// </summary>
        /// <param name="keepAlive">Whether channels are kept alive when empty, or empty to reset.</param>
        [SlashCommand("keep-alive", "Set whether your channels are kept alive when empty")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsKeepAlive(
            [Summary("enabled", "Keep alive, leave empty to reset")]
            bool? keepAlive = null)
        {
            var prefs = await GetPrefsOrErrorAsync();
            if (prefs is null)
                return;

            if (keepAlive == null)
            {
                prefs.KeepAlive = null;
                await Service.SetUserPreferencesAsync(prefs);
                await ReplyConfirmAsync(Strings.CustomVoicePrefsKeepAliveReset(ctx.Guild.Id));
                return;
            }

            prefs.KeepAlive = keepAlive;
            await Service.SetUserPreferencesAsync(prefs);

            var keepAliveStatus = keepAlive.Value
                ? Strings.CustomVoicePrefsKeptAlive(ctx.Guild.Id)
                : Strings.CustomVoicePrefsNotKeptAlive(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.CustomVoicePrefsKeepAliveSet(ctx.Guild.Id, keepAliveStatus));
        }

        /// <summary>
        ///     Resets all your voice channel preferences.
        /// </summary>
        [SlashCommand("reset", "Reset all your voice channel preferences")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VoicePrefsReset()
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var prefs = await dbContext.UserVoicePreferences
                .FirstOrDefaultAsync(p => p.GuildId == ctx.Guild.Id && p.UserId == ctx.User.Id);

            if (prefs != null)
            {
                await dbContext.UserVoicePreferences
                    .Where(p => p.GuildId == ctx.Guild.Id && p.UserId == ctx.User.Id)
                    .DeleteAsync();
                await ReplyConfirmAsync(Strings.CustomVoicePrefsAllReset(ctx.Guild.Id));
            }
            else
            {
                await ReplyConfirmAsync(Strings.CustomVoicePrefsNoneExist(ctx.Guild.Id));
            }
        }
    }
}