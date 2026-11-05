using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for the colour of this server's ok and info embeds.
    /// </summary>
    [Group]
    public class ServerColorCommands : MewdekoSubmodule<GuildEmbedColorService>
    {
        /// <summary>
        ///     Shows, sets or clears the colour of this server's ok and info embeds.
        /// </summary>
        /// <param name="color">A hex code, a colour name, or reset to use the default again.</param>
        /// <example>.servercolor #ff66aa</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ServerColor([Remainder] string? color = null)
        {
            var guildId = ctx.Guild.Id;
            if (string.IsNullOrWhiteSpace(color))
            {
                var current = Service.Get(guildId);
                var text = current is { } own
                    ? Strings.ServerColorCurrent(guildId, $"#{own.RawValue:X6}")
                    : Strings.ServerColorDefault(guildId);
                await ReplyConfirmAsync(text).ConfigureAwait(false);
                return;
            }

            if (color.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                color.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                await Service.SetAsync(guildId, null).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.ServerColorReset(guildId)).ConfigureAwait(false);
                return;
            }

            if (!ColorUtils.TryParseColor(color, out var parsed))
            {
                await ReplyErrorAsync(Strings.ServerColorInvalid(guildId)).ConfigureAwait(false);
                return;
            }

            await Service.SetAsync(guildId, parsed).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ServerColorSet(guildId, $"#{parsed.RawValue:X6}")).ConfigureAwait(false);
        }
    }
}
