using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Modules.CustomReactions
{
    public class ExportedExpr
    {
        public string Res { get; set; }
        public bool Ad { get; set; }
        public bool Dm { get; set; }
        public bool At { get; set; }
        public bool Ca { get; set; }
        public string[] React;

        public static ExportedExpr FromModel(CustomReaction cr)
            => new ExportedExpr()
            {
                Res = cr.Response,
                Ad = cr.AutoDeleteTrigger,
                At = cr.AllowTarget,
                Ca = cr.ContainsAnywhere,
                Dm = cr.DmResponse,
                React = string.IsNullOrWhiteSpace(cr.Reactions)
                    ? null
                    : cr.GetReactions(),
            };
    }
}
