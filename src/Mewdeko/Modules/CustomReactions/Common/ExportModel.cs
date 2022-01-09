using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.CustomReactions.Common;

public class ExportedExpr
{
    public string[] React;
    public string Res { get; set; }
    public bool Ad { get; set; }
    public bool Dm { get; set; }
    public bool At { get; set; }
    public bool Ca { get; set; }
    public bool Rtt { get; set; }
    public bool Nr { get; set; }

    public static ExportedExpr FromModel(CustomReaction cr) =>
        new()
        {
            Res = cr.Response,
            Ad = cr.AutoDeleteTrigger,
            At = cr.AllowTarget,
            Ca = cr.ContainsAnywhere,
            Dm = cr.DmResponse,
            React = string.IsNullOrWhiteSpace(cr.Reactions)
                ? null
                : cr.GetReactions(),
            Rtt = cr.ReactToTrigger,
            Nr = cr.NoRespond
        };
}