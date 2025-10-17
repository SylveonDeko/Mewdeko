using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.CustomVoice.Services;

namespace Mewdeko.Modules.CustomVoice;

/// <summary>
///     Interaction handlers for custom voice channel controls.
/// </summary>
public class CustomVoiceInteractions : MewdekoSlashModuleBase<CustomVoiceService>
{
    /// <summary>
    ///     Handles button interactions for voice channel controls.
    /// </summary>
    [ComponentInteraction("voice:*:*")]
    public async Task HandleVoiceButton(string action, string channelIdStr)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        // Get the custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, channelId);
        if (customChannel == null)
        {
            await FollowupAsync("This channel is no longer a custom voice channel.", ephemeral: true);
            return;
        }

        // Check if user is the owner or has admin permissions
        var user = Context.User as IGuildUser;
        var isOwner = customChannel.OwnerId == user.Id;
        var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue &&
                           user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

        if (!isOwner && !hasAdminRole)
        {
            await FollowupAsync("You don't have permission to control this channel.", ephemeral: true);
            return;
        }

        // Handle different actions
        switch (action)
        {
            case "rename":
                await HandleRename(channelId);
                break;

            case "limit":
                await HandleUserLimit(channelId);
                break;

            case "bitrate":
                await HandleBitrate(channelId);
                break;

            case "lock":
                await HandleLock(channelId, true);
                break;

            case "unlock":
                await HandleLock(channelId, false);
                break;

            case "delete":
                await HandleDelete(channelId);
                break;

            case "transfer":
                await HandleTransfer(channelId);
                break;

            case "manage":
                await HandleManageUsers(channelId);
                break;

            default:
                await FollowupAsync("Unknown action.", ephemeral: true);
                break;
        }
    }

    /// <summary>
    ///     Handles the keep alive button with toggle value.
    /// </summary>
    [ComponentInteraction("voice:keepalive:*:*")]
    public async Task HandleKeepAlive(string channelIdStr, string keepAliveStr)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        if (!bool.TryParse(keepAliveStr, out var keepAlive))
        {
            await FollowupAsync("Invalid keep alive value.", ephemeral: true);
            return;
        }

        // Get the custom voice channel
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, channelId);
        if (customChannel == null)
        {
            await FollowupAsync("This channel is no longer a custom voice channel.", ephemeral: true);
            return;
        }

        // Check permissions
        var user = Context.User as IGuildUser;
        var isOwner = customChannel.OwnerId == user.Id;
        var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue &&
                           user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

        if (!isOwner && !hasAdminRole)
        {
            await FollowupAsync("You don't have permission to control this channel.", ephemeral: true);
            return;
        }

        // Toggle keep alive
        customChannel.KeepAlive = keepAlive;
        await Service.SetUserPreferencesAsync(new UserVoicePreference
        {
            GuildId = Context.Guild.Id, UserId = user.Id, KeepAlive = keepAlive
        });

        await FollowupAsync(
            keepAlive ? "Channel will be kept alive when empty." : "Channel will be deleted when empty.",
            ephemeral: true);
    }

    private async Task HandleRename(ulong channelId)
    {
        var modal = new ModalBuilder()
            .WithTitle("Rename Voice Channel")
            .WithCustomId($"voice:rename:modal:{channelId}")
            .AddTextInput("New Channel Name", "name", TextInputStyle.Short, "Enter new name", 1, 100, true)
            .Build();

        await RespondWithModalAsync(modal);
    }

    private async Task HandleUserLimit(ulong channelId)
    {
        var modal = new ModalBuilder()
            .WithTitle("Set User Limit")
            .WithCustomId($"voice:limit:modal:{channelId}")
            .AddTextInput("User Limit", "limit", TextInputStyle.Short, "0 for unlimited", 1, 3, true)
            .Build();

        await RespondWithModalAsync(modal);
    }

    private async Task HandleBitrate(ulong channelId)
    {
        var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        var modal = new ModalBuilder()
            .WithTitle("Set Bitrate")
            .WithCustomId($"voice:bitrate:modal:{channelId}")
            .AddTextInput("Bitrate (kbps)", "bitrate", TextInputStyle.Short,
                $"Max: {config.MaxBitrate} kbps", 1, 6, true)
            .Build();

        await RespondWithModalAsync(modal);
    }

    private async Task HandleLock(ulong channelId, bool locked)
    {
        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, channelId, isLocked: locked))
        {
            await FollowupAsync(locked ? "Channel locked." : "Channel unlocked.", ephemeral: true);
        }
        else
        {
            await FollowupAsync("Failed to update channel.", ephemeral: true);
        }
    }

    private async Task HandleDelete(ulong channelId)
    {
        var components = new ComponentBuilder()
            .WithButton("Confirm Delete", $"voice:delete:confirm:{channelId}", ButtonStyle.Danger)
            .WithButton("Cancel", $"voice:delete:cancel:{channelId}", ButtonStyle.Secondary)
            .Build();

        await FollowupAsync("Are you sure you want to delete this channel?", components: components, ephemeral: true);
    }

    private async Task HandleTransfer(ulong channelId)
    {
        await FollowupAsync(
            "To transfer ownership, use the dropdown menu to select a user, or use the `.voicetransfer @user` command.",
            ephemeral: true);
    }

    private async Task HandleManageUsers(ulong channelId)
    {
        await FollowupAsync(
            "To manage users, use the `.voiceallow @user` or `.voicedeny @user` commands, or use the `.vcontrols` command for a full user management interface.",
            ephemeral: true);
    }

    /// <summary>
    ///     Handles delete confirmation.
    /// </summary>
    [ComponentInteraction("voice:delete:confirm:*")]
    public async Task HandleDeleteConfirm(string channelIdStr)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        // Check permissions
        var customChannel = await Service.GetChannelAsync(Context.Guild.Id, channelId);
        if (customChannel == null)
        {
            await FollowupAsync("This channel no longer exists.", ephemeral: true);
            return;
        }

        var user = Context.User as IGuildUser;
        var isOwner = customChannel.OwnerId == user.Id;
        var config = await Service.GetOrCreateConfigAsync(Context.Guild.Id);
        var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue &&
                           user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

        if (!isOwner && !hasAdminRole)
        {
            await FollowupAsync("You don't have permission to delete this channel.", ephemeral: true);
            return;
        }

        if (await Service.DeleteVoiceChannelAsync(Context.Guild.Id, channelId))
        {
            await FollowupAsync("Channel deleted successfully.", ephemeral: true);
        }
        else
        {
            await FollowupAsync("Failed to delete channel.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles delete cancellation.
    /// </summary>
    [ComponentInteraction("voice:delete:cancel:*")]
    public async Task HandleDeleteCancel(string channelIdStr)
    {
        await RespondAsync("Channel deletion cancelled.", ephemeral: true);
    }

    /// <summary>
    ///     Handles rename modal submission.
    /// </summary>
    [ModalInteraction("voice:rename:modal:*")]
    public async Task HandleRenameModal(string channelIdStr, RenameVoiceChannelModal modal)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, channelId, modal.Name))
        {
            await FollowupAsync($"Channel renamed to **{modal.Name}**.", ephemeral: true);
        }
        else
        {
            await FollowupAsync("Failed to rename channel.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles user limit modal submission.
    /// </summary>
    [ModalInteraction("voice:limit:modal:*")]
    public async Task HandleLimitModal(string channelIdStr, VoiceChannelLimitModal modal)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        if (!int.TryParse(modal.Limit, out var limit) || limit < 0)
        {
            await FollowupAsync("Invalid user limit. Must be 0 or greater.", ephemeral: true);
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, channelId, userLimit: limit))
        {
            var limitText = limit == 0 ? "unlimited" : limit.ToString();
            await FollowupAsync($"User limit set to **{limitText}**.", ephemeral: true);
        }
        else
        {
            await FollowupAsync("Failed to update user limit.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Handles bitrate modal submission.
    /// </summary>
    [ModalInteraction("voice:bitrate:modal:*")]
    public async Task HandleBitrateModal(string channelIdStr, VoiceChannelBitrateModal modal)
    {
        await DeferAsync(true);

        if (!ulong.TryParse(channelIdStr, out var channelId))
        {
            await FollowupAsync("Invalid channel ID.", ephemeral: true);
            return;
        }

        if (!int.TryParse(modal.Bitrate, out var bitrate) || bitrate <= 0)
        {
            await FollowupAsync("Invalid bitrate. Must be greater than 0.", ephemeral: true);
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(Context.Guild.Id, channelId, bitrate: bitrate))
        {
            await FollowupAsync($"Bitrate set to **{bitrate} kbps**.", ephemeral: true);
        }
        else
        {
            await FollowupAsync("Failed to update bitrate.", ephemeral: true);
        }
    }
}