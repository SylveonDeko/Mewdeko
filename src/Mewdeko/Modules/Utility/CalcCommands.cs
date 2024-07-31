using System.Reflection;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using NCalc;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for evaluating mathematical expressions.
    /// </summary>
    /// <param name="guildSettings">The guild settings service.</param>
    [Group]
    public class CalcCommands(GuildSettingsService guildSettings) : MewdekoSubmodule
    {
        /// <summary>
        /// Evaluates a mathematical expression and returns the result.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases]
        public async Task Calculate([Remainder] string expression)
        {
            var expr = new Expression(expression, EvaluateOptions.IgnoreCase | EvaluateOptions.NoCache);
            expr.EvaluateParameter += Expr_EvaluateParameter;
            var result = expr.Evaluate();
            if (!expr.HasErrors())
            {
                await ctx.Channel.SendConfirmAsync($"⚙ {GetText("result")}", result.ToString())
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendErrorAsync($"⚙ {GetText("error")}", expr.Error).ConfigureAwait(false);
            }
        }

        private static void Expr_EvaluateParameter(string name, ParameterArgs args) =>
            args.Result = name.ToLowerInvariant() switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                _ => args.Result
            };

        /// <summary>
        /// Lists available mathematical operations that can be used in expressions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases]
        public async Task CalcOps()
        {
            var selection = typeof(Math).GetTypeInfo()
                .GetMethods()
                .Distinct(new MethodInfoEqualityComparer())
                .Select(x => x.Name)
                .Except([
                    "ToString", "Equals", "GetHashCode", "GetType"
                ]);
            await ctx.Channel.SendConfirmAsync(GetText("calcops", await guildSettings.GetPrefix(ctx.Guild)),
                    string.Join(", ", selection))
                .ConfigureAwait(false);
        }
    }

    private class MethodInfoEqualityComparer : IEqualityComparer<MethodInfo>
    {
        public bool Equals(MethodInfo x, MethodInfo y) => x.Name == y.Name;

        public int GetHashCode(MethodInfo obj) => obj.Name.GetHashCode(StringComparison.InvariantCulture);
    }
}