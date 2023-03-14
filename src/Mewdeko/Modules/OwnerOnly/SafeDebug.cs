using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Modules.OwnerOnly;

public partial class OwnerOnly
{
    [Group]
    public class SafeDebug : MewdekoSubmodule<OwnerOnlyService>
    {
        [Cmd, Aliases]
        public async Task TestLocalize([Remainder] string input)
        {
            var sp = input.Split("|");
            if (sp[0].IsNullOrWhiteSpace())
            {
                await ErrorLocalizedAsync("__loctest_invalid");
                return;
            }
            await ConfirmLocalizedAsync(sp[0], sp.Skip(1).ToArray());
        }
    }
}
