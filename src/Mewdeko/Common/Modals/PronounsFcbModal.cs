using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class PronounsFcbModal : IModal
{
    public string Title => "PNOFC/B";

    [ModalTextInput("fcb_reason", TextInputStyle.Short, initValue: "An anti-abuse report was actioned by a moderator.")]
    [InputLabel("Reason")]
    public string FcbReason { get; set; }
}