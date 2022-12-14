using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class PronounsModal : IModal
{
    public string Title => "Set your pronouns";

    [ModalTextInput("pronouns")]
    [InputLabel("Pronouns")]
    public string Pronouns { get; set; }
}