using Mewdeko.Database.Extensions;

namespace Mewdeko.Modules.Chat_Triggers.Common;

public class ExportedTriggers
{
    public string[] React;
    public List<ulong> aRole;
    public List<ulong> rRole;
    public string Res { get; set; }
    public bool Ad { get; set; }
    public bool Dm { get; set; }
    public bool At { get; set; }
    public bool Ca { get; set; }
    public bool Rtt { get; set; }
    public bool Nr { get; set; }
    public bool Rgx { get; set; }

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
            rRole = ct.GetRemovedRoles(),
            aRole = ct.GetGrantedRoles()
        };
}
