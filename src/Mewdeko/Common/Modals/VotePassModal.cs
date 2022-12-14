using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class VotePasswordModal : IModal
{
    public string Title => "Vote Password";

    [InputLabel("VotePassword")]
    [ModalTextInput("votepassword", TextInputStyle.Paragraph, "Please enter the password. DO NOT SHARE THIS TO ANYONE ELSE"), RequiredInput(false)]
    public string Password { get; set; }
}