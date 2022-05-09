using Discord;
using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class PronounsFCBlacklistModal : IModal
{
    public string Title => "Pronouns Force Clear and Blacklist";
    
    [ModalTextInput("fc_reason", TextInputStyle.Short, initValue: "An anti-abuse report was actioned by a moderator.")]
    [InputLabel("Force Clear Reason")]
    public string FcReason { get; set; }
    
    [ModalTextInput("bl_reason", TextInputStyle.Short, initValue: "An anti-abuse report related to the pronoun system was actioned by a moderator.")]
    [InputLabel("Blacklist Reason")]
    public string BlacklistReason { get; set; }
}