using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for renaming a voice channel.
/// </summary>
public class RenameVoiceChannelModal : IModal
{
    /// <summary>
    ///     Gets or sets the new channel name.
    /// </summary>
    [InputLabel("New Channel Name")]
    [ModalTextInput("name", TextInputStyle.Short, "Enter new name", 1, 100)]
    public string Name { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Rename Voice Channel";
        }
    }
}

/// <summary>
///     Modal for setting user limit.
/// </summary>
public class VoiceChannelLimitModal : IModal
{
    /// <summary>
    ///     Gets or sets the user limit.
    /// </summary>
    [InputLabel("User Limit")]
    [ModalTextInput("limit", TextInputStyle.Short, "0 for unlimited", 1, 3)]
    public string Limit { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Set User Limit";
        }
    }
}

/// <summary>
///     Modal for setting bitrate.
/// </summary>
public class VoiceChannelBitrateModal : IModal
{
    /// <summary>
    ///     Gets or sets the bitrate in kbps.
    /// </summary>
    [InputLabel("Bitrate (kbps)")]
    [ModalTextInput("bitrate", TextInputStyle.Short, "Enter bitrate in kbps", 1, 6)]
    public string Bitrate { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Set Bitrate";
        }
    }
}