using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.CustomVoice.Services;

namespace Mewdeko.Modules.CustomVoice;

/// <summary>
///     Interaction handlers for deleting a custom voice channel from its controls. The control buttons themselves
///     are handled by <see cref="CustomVoiceSlash" />.
/// </summary>
public class CustomVoiceInteractions : MewdekoSlashModuleBase<CustomVoiceService>
{
    /// <summary>
    ///     Handles delete confirmation.
    /// </summary>
    [ComponentInteraction("voice:deleteconfirm:*")]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
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
        var config = await Service.GetConfigForChannelAsync(Context.Guild.Id, channelId);
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
    [ComponentInteraction("voice:deletecancel:*")]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleDeleteCancel(string channelIdStr)
    {
        await RespondAsync("Channel deletion cancelled.", ephemeral: true);
    }
}
