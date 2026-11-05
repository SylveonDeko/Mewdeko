using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Administration;

public partial class SlashServerTools
{
    /// <summary>
    ///     Slash commands for the colour of this server's ok and info embeds.
    /// </summary>
    [Group("embed-color", "The colour of this server's embeds")]
    public class SlashServerColor : MewdekoSlashModuleBase<GuildEmbedColorService>
    {
        /// <summary>
        ///     Shows the colour of this server's ok and info embeds.
        /// </summary>
        [SlashCommand("show", "Show the colour of this server's embeds")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Show()
        {
            var guildId = ctx.Guild.Id;
            var text = Service.Get(guildId) is { } own
                ? Strings.ServerColorCurrent(guildId, $"#{own.RawValue:X6}")
                : Strings.ServerColorDefault(guildId);
            await ReplyConfirmAsync(text).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the colour of this server's ok and info embeds.
        /// </summary>
        /// <param name="color">A hex code or a colour name.</param>
        [SlashCommand("set", "Set the colour of this server's embeds")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Set([Summary("color", "A hex code such as #ff66aa, or a colour name")] string color)
        {
            var guildId = ctx.Guild.Id;
            if (!ColorUtils.TryParseColor(color, out var parsed))
            {
                await ReplyErrorAsync(Strings.ServerColorInvalid(guildId)).ConfigureAwait(false);
                return;
            }

            await Service.SetAsync(guildId, parsed).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ServerColorSet(guildId, $"#{parsed.RawValue:X6}")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Goes back to the default embed colour.
        /// </summary>
        [SlashCommand("reset", "Go back to the default embed colour")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Reset()
        {
            await Service.SetAsync(ctx.Guild.Id, null).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.ServerColorReset(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}
