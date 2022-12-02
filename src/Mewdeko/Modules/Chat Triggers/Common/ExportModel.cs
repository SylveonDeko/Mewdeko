namespace Mewdeko.Modules.Chat_Triggers.Common;

public class ExportedTriggers
{
    public string[]? React;
    public List<ulong> ARole;
    public List<ulong> RRole;
    public string Res { get; set; }
    public bool Ad { get; set; }
    public bool Dm { get; set; }
    public bool At { get; set; }
    public bool Ca { get; set; }
    public bool Rtt { get; set; }
    public bool Nr { get; set; }
    public bool Rgx { get; set; }
    public CtRoleGrantType Rgt { get; set; }
    public ChatTriggerType VTypes { get; set; }
    public string AcName { get; set; }
    public string AcDesc { get; set; }
    public CtApplicationCommandType Act { get; set; }
    public bool Eph { get; set; }

    public static ExportedTriggers FromModel(Database.Models.ChatTriggers ct) =>
        new()
        {
            Res = ct.Response,
            Ad = ct.AutoDeleteTrigger,
            At = ct.AllowTarget,
            Ca = ct.ContainsAnywhere,
            Dm = ct.DmResponse,
            Rgx = ct.IsRegex,
            React = string.IsNullOrWhiteSpace(ct.Reactions)
                ? null
                : ct.GetReactions(),
            Rtt = ct.ReactToTrigger,
            Nr = ct.NoRespond,
            RRole = ct.GetRemovedRoles(),
            ARole = ct.GetGrantedRoles(),
            Rgt = ct.RoleGrantType,
            VTypes = ct.ValidTriggerTypes,
            AcName = ct.ApplicationCommandName,
            AcDesc = ct.ApplicationCommandDescription,
            Act = ct.ApplicationCommandType,
            Eph = ct.EphemeralResponse
        };
}