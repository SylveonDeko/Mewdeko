using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using NCalc;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class CalcCommands : MewdekoSubmodule
    {
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Calculate([Remainder] string expression)
        {
            var expr = new Expression(expression, EvaluateOptions.IgnoreCase | EvaluateOptions.NoCache);
            expr.EvaluateParameter += Expr_EvaluateParameter;
            var result = expr.Evaluate();
            if (!expr.HasErrors())
                await ctx.Channel.SendConfirmAsync("⚙ " + GetText("result"), result.ToString())
                    .ConfigureAwait(false);
            else
                await ctx.Channel.SendErrorAsync("⚙ " + GetText("error"), expr.Error).ConfigureAwait(false);
        }

        private static void Expr_EvaluateParameter(string name, ParameterArgs args)
        {
            switch (name.ToLowerInvariant())
            {
                case "pi":
                    args.Result = Math.PI;
                    break;
                case "e":
                    args.Result = Math.E;
                    break;
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task CalcOps()
        {
            var selection = typeof(Math).GetTypeInfo()
                .GetMethods()
                .Distinct(new MethodInfoEqualityComparer())
                .Select(x => x.Name)
                .Except(new[]
                {
                    "ToString",
                    "Equals",
                    "GetHashCode",
                    "GetType"
                });
            await ctx.Channel.SendConfirmAsync(GetText("calcops", Prefix), string.Join(", ", selection))
                .ConfigureAwait(false);
        }
    }

    private class MethodInfoEqualityComparer : IEqualityComparer<MethodInfo>
    {
        public bool Equals(MethodInfo x, MethodInfo y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(MethodInfo obj)
        {
            return obj.Name.GetHashCode(StringComparison.InvariantCulture);
        }
    }
}