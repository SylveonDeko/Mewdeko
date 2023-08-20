namespace Mewdeko.Modules.Chat_Triggers.Common;

public class ExportedTriggers
{
    public string[]? React;
    public List<ulong> ARole = new();

    public List<ulong> RRole = new();

    // here for backwards compatibility with nadeko lol
    public string Id { get; set; }
    public string Res { get; set; }
    public bool Ad { get; set; }
    public bool Dm { get; set; }
    public bool At { get; set; }
    public bool Ca { get; set; }
    public bool Rtt { get; set; }
    public bool Nr { get; set; }
    public bool Rgx { get; set; }
    public CtRoleGrantType Rgt { get; set; }
    public ChatTriggerType VTypes { get; set; } = ChatTriggerType.Message;
    public string AcName { get; set; } = "";
    public string AcDesc { get; set; } = "";
    public CtApplicationCommandType Act { get; set; }
    public bool Eph { get; set; }

    public static ExportedTriggers FromModel(Database.Models.ChatTriggers ct) =>
        new()
        {
            Id = "",
            Res = ct.Response,
            Ad = ct.AutoDeleteTrigger == 1,
            At = ct.AllowTarget == 1,
            Ca = ct.ContainsAnywhere == 1,
            Dm = ct.DmResponse == 1,
            Rgx = ct.IsRegex == 1,
            React = string.IsNullOrWhiteSpace(ct.Reactions)
                ? null
                : ct.GetReactions(),
            Rtt = ct.ReactToTrigger == 1,
            Nr = ct.NoRespond == 1,
            RRole = ct.GetRemovedRoles(),
            ARole = ct.GetGrantedRoles(),
            Rgt = ct.RoleGrantType,
            VTypes = ct.ValidTriggerTypes,
            AcName = ct.ApplicationCommandName,
            AcDesc = ct.ApplicationCommandDescription,
            Act = ct.ApplicationCommandType,
            Eph = ct.EphemeralResponse == 1
        };
}