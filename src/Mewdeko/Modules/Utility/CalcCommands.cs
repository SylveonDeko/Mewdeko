using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using MathNet.Symbolics;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Provides calculator functionality including expression evaluation, graphing, and symbolic math.
    /// </summary>
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
            try
            {
                var result = Evaluate(expression);
                await ctx.Channel.SendConfirmAsync($"⚙ {GetText("calc_result")}", result.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendErrorAsync($"⚙ {GetText("calc_error")}", ex.Message).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Graphs a mathematical function.
        /// </summary>
        /// <param name="function">The function to graph.</param>
        /// <param name="start">The start of the x-axis range.</param>
        /// <param name="end">The end of the x-axis range.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases]
        public async Task Graph(string function, double start = -10, double end = 10)
        {
            try
            {
                var plotModel = new PlotModel { Title = GetText("graph_title", function) };
                plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "X" });
                plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Y" });

                var series = new FunctionSeries(
                    x => Evaluate(function.Replace("x", x.ToString())),
                    start, end, 0.1, function);
                plotModel.Series.Add(series);

                using var stream = new System.IO.MemoryStream();
                var pngExporter = new OxyPlot.SkiaSharp.PngExporter { Width = 600, Height = 400 };
                pngExporter.Export(plotModel, stream);
                stream.Position = 0;

                await ctx.Channel.SendFileAsync(stream, "graph.png", GetText("graph_caption", function)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendErrorAsync($"⚙ {GetText("graph_error")}", ex.Message).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs symbolic mathematics operations.
        /// </summary>
        /// <param name="expression">The symbolic expression to evaluate or manipulate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases]
        public async Task Symbolic([Remainder] string expression)
        {
            try
            {
                var expr = Infix.ParseOrThrow(expression);
                var expanded = Algebraic.Expand(expr);
                await ctx.Channel.SendConfirmAsync($"⚙ {GetText("symbolic_result")}", Infix.Format(expanded)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendErrorAsync($"⚙ {GetText("symbolic_error")}", ex.Message).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Lists available mathematical operations that can be used in expressions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases]
        public async Task CalcOps()
        {
            var operations = new[]
            {
                "+", "-", "*", "/", "^", "sqrt", "abs",
                "sin", "cos", "tan", "asin", "acos", "atan",
                "log", "ln", "exp", "floor", "ceiling", "round"
            };

            var prefix = await guildSettings.GetPrefix(ctx.Guild);
            var message = GetText("calcops", prefix) + "\n" + string.Join(", ", operations);

            await ctx.Channel.SendConfirmAsync("⚙ " + GetText("available_operations"), message).ConfigureAwait(false);
        }

        private static double Evaluate(string expression)
        {
            var expr = Infix.ParseOrThrow(expression);
            var variables = new Dictionary<string, FloatingPoint>();
            var result = MathNet.Symbolics.Evaluate.Evaluate(variables, expr);

            if (result != null)
            {
                return result.RealValue;
            }

            throw new InvalidOperationException($"Unable to evaluate expression to a numeric value: {expression}");
        }
    }
}