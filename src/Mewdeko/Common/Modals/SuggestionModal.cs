using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class SuggestionModal : IModal
{
    public string Title => "Suggestion";

    [InputLabel("Suggestion")]
    [ModalTextInput("suggestion", TextInputStyle.Paragraph, "Please enter suggestion")]
    public string Suggestion { get; set; }
}