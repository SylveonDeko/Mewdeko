using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Modules.Administration.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

#if !GLOBAL_Mewdeko
namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        [OwnerOnly]
        public class DangerousCommands : MewdekoSubmodule<DangerousCommandsService>
        {
            private readonly DiscordSocketClient _client;

            public DangerousCommands(DiscordSocketClient client)
            {
                _client = client;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task Bash([Remainder] string message)
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{message} 2>&1\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (ctx.Channel.EnterTypingState())
                    {
                        process.Start();

                        // Synchronously read the standard output of the spawned process.
                        var reader = process.StandardOutput;

                        var output = await reader.ReadToEndAsync();
                        if (output.Length > 2000)
                        {
                            var chunkSize = 1988;
                            var stringLength = output.Length;
                            for (var i = 0; i < stringLength; i += chunkSize)
                            {
                                if (i + chunkSize > stringLength) chunkSize = stringLength - i;
                                await ctx.Channel.SendMessageAsync($"```bash\n{output.Substring(i, chunkSize)}```");
                                process.WaitForExit();
                            }
                        }
                        else if (output == "")
                        {
                            await ctx.Channel.SendMessageAsync("```The output was blank```");
                        }
                        else
                        {
                            await ctx.Channel.SendMessageAsync("```bash\n" + output + "```");
                        }
                    }

                    process.WaitForExit();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Alias]
            [OwnerOnly]
            public async Task Evaluate([Remainder] string code)
            {
                var cs1 = code.IndexOf("```") + 3;
                cs1 = code.IndexOf('\n', cs1) + 1;
                var cs2 = code.LastIndexOf("```");

                if (cs1 == -1 || cs2 == -1)
                    throw new ArgumentException("You need to wrap the code into a code block.", nameof(code));

                code = code.Substring(cs1, cs2 - cs1);

                var embed = new EmbedBuilder
                {
                    Title = "Evaluating...",
                    Color = new Color(0xD091B2)
                };
                var msg = await ctx.Channel.SendMessageAsync("", embed: embed.Build());

                var globals = new EvaluationEnvironment((CommandContext)Context);
                var sopts = ScriptOptions.Default
                    .WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
                        "System.Net.Http", "System.Net.Http.Headers", "System.Reflection", "System.Text",
                        "System.Threading.Tasks", "Discord.Net", "Discord", "Discord.WebSocket", "Mewdeko.Modules",
                        "Mewdeko.Core.Services", "Mewdeko.Extensions", "Mewdeko.Modules.Administration",
                        "Mewdeko.Modules.CustomReactions", "Mewdeko.Modules.Gambling", "Mewdeko.Modules.Games",
                        "Mewdeko.Modules.Help", "Mewdeko.Modules.Music", "Mewdeko.Modules.NSFW",
                        "Mewdeko.Modules.Permissions", "Mewdeko.Modules.Searches", "Mewdeko.Modules.ServerManagement")
                    .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                        .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));

                var sw1 = Stopwatch.StartNew();
                var cs = CSharpScript.Create(code, sopts, typeof(EvaluationEnvironment));
                var csc = cs.Compile();
                sw1.Stop();

                if (csc.Any(xd => xd.Severity == DiagnosticSeverity.Error))
                {
                    embed = new EmbedBuilder
                    {
                        Title = "Compilation failed",
                        Description = string.Concat("Compilation failed after ",
                            sw1.ElapsedMilliseconds.ToString("#,##0"), "ms with ", csc.Length.ToString("#,##0"),
                            " errors."),
                        Color = new Color(0xD091B2)
                    };
                    foreach (var xd in csc.Take(3))
                    {
                        var ls = xd.Location.GetLineSpan();
                        embed.AddField(
                            string.Concat("Error at ", ls.StartLinePosition.Line.ToString("#,##0"), ", ",
                                ls.StartLinePosition.Character.ToString("#,##0")), Format.Code(xd.GetMessage()));
                    }

                    if (csc.Length > 3)
                        embed.AddField("Some errors ommited",
                            string.Concat((csc.Length - 3).ToString("#,##0"), " more errors not displayed"));
                    await msg.ModifyAsync(x => x.Embed = embed.Build());
                    return;
                }

                Exception rex = null;
                ScriptState<object> css = null;
                var sw2 = Stopwatch.StartNew();
                try
                {
                    css = await cs.RunAsync(globals);
                    rex = css.Exception;
                }
                catch (Exception ex)
                {
                    rex = ex;
                }

                sw2.Stop();

                if (rex != null)
                {
                    embed = new EmbedBuilder
                    {
                        Title = "Execution failed",
                        Description = string.Concat("Execution failed after ",
                            sw2.ElapsedMilliseconds.ToString("#,##0"), "ms with `", rex.GetType(), ": ", rex.Message,
                            "`."),
                        Color = new Color(0xD091B2)
                    };
                    await msg.ModifyAsync(x => { x.Embed = embed.Build(); });
                    return;
                }

                // execution succeeded
                embed = new EmbedBuilder
                {
                    Title = "Evaluation successful",
                    Color = new Color(0xD091B2)
                };

                embed.AddField("Result", css.ReturnValue != null ? css.ReturnValue.ToString() : "No value returned")
                    .AddField("Compilation time", string.Concat(sw1.ElapsedMilliseconds.ToString("#,##0"), "ms"), true)
                    .AddField("Execution time", string.Concat(sw2.ElapsedMilliseconds.ToString("#,##0"), "ms"), true);

                if (css.ReturnValue != null)
                    embed.AddField("Return type", css.ReturnValue.GetType().ToString(), true);

                await msg.ModifyAsync(x => { x.Embed = embed.Build(); });
            }


            //[MewdekoCommand, Usage, Description, Aliases]
            //[OwnerOnly]
            //public Task DeleteUnusedCrnQ() =>
            //    SqlExec(DangerousCommandsService.DeleteUnusedCustomReactionsAndQuotes);
        }

        public sealed class EvaluationEnvironment
        {
            public EvaluationEnvironment(CommandContext ctx)
            {
                this.ctx = ctx;
            }

            public CommandContext ctx { get; }

            public IUserMessage Message => ctx.Message;
            public IMessageChannel Channel => ctx.Channel;
            public IGuild Guild => ctx.Guild;
            public IUser User => ctx.User;
            public IGuildUser Member => (IGuildUser)ctx.User;
            public IDiscordClient Client => ctx.Client;
        }
    }
}
#endif