using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Provides commands for translating text and managing auto-translation settings within a Discord guild.
    /// </summary>
    [Group]
    public class TranslateCommands(SearchesService searches, IGoogleApiService google) : MewdekoSubmodule
    {
        /// <summary>
        ///     Enumeration for auto-delete options in auto-translate feature.
        /// </summary>
        public enum AutoDeleteAutoTranslate
        {
            /// <summary>
            ///     Deletes the original message after translating it.
            /// </summary>
            Del,

            /// <summary>
            ///     Leaves the original message after translating it.
            /// </summary>
            Nodel
        }

        /// <summary>
        ///     Translates text from one language to another specified by the user.
        /// </summary>
        /// <param name="langs">The language pair in the format 'from>to'.</param>
        /// <param name="text">The text to be translated.</param>
        /// <remarks>
        ///     This command uses an external API to translate the provided text from one language to another.
        ///     The language pair should be specified in the format 'from>to', where 'from' and 'to' are language codes.
        /// </remarks>
        [Cmd]
        [Aliases]
        public async Task Translate(string langs, [Remainder] string? text = null)
        {
            try
            {
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                var translation = await SearchesService.Translate(langs, text).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"{GetText("translation")} {langs}", translation)
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("bad_input_format").ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles auto-translation for the current channel, optionally enabling auto-deletion of the original message.
        /// </summary>
        /// <param name="autoDelete">Option to auto-delete the original message after translation.</param>
        /// <remarks>
        ///     This command enables or disables automatic translation of all messages sent in the current channel.
        ///     If auto-delete is enabled, the original message is deleted after being translated.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutoTranslate(AutoDeleteAutoTranslate autoDelete = AutoDeleteAutoTranslate.Nodel)
        {
            var channel = (ITextChannel)ctx.Channel;

            if (autoDelete == AutoDeleteAutoTranslate.Del)
            {
                searches.TranslatedChannels.AddOrUpdate(channel.Id, true, (_, _) => true);
                await ReplyConfirmLocalizedAsync("atl_ad_started").ConfigureAwait(false);
                return;
            }

            if (searches.TranslatedChannels.TryRemove(channel.Id, out _))
            {
                await ReplyConfirmLocalizedAsync("atl_stopped").ConfigureAwait(false);
                return;
            }

            if (searches.TranslatedChannels.TryAdd(channel.Id, autoDelete == AutoDeleteAutoTranslate.Del))
                await ReplyConfirmLocalizedAsync("atl_started").ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or removes the preferred language pair for auto-translation for the user in the current channel.
        /// </summary>
        /// <param name="langs">The language pair in the format 'from>to', or null to remove the setting.</param>
        /// <remarks>
        ///     Users can set their preferred languages for auto-translation in the current channel.
        ///     To remove the preference, the command can be called without specifying a language pair.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AutoTransLang([Remainder] string? langs = null)
        {
            var ucp = (ctx.User.Id, ctx.Channel.Id);

            if (string.IsNullOrWhiteSpace(langs))
            {
                if (searches.UserLanguages.TryRemove(ucp, out langs))
                    await ReplyConfirmLocalizedAsync("atl_removed").ConfigureAwait(false);
                return;
            }

            var langarr = langs.ToLowerInvariant().Split('>');
            if (langarr.Length != 2)
                return;
            var from = langarr[0];
            var to = langarr[1];

            if (!google.Languages.Contains(from) || !google.Languages.Contains(to))
            {
                await ReplyErrorLocalizedAsync("invalid_lang").ConfigureAwait(false);
                return;
            }

            searches.UserLanguages.AddOrUpdate(ucp, langs, (_, _) => langs);

            await ReplyConfirmLocalizedAsync("atl_set", from, to).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all available languages for translation.
        /// </summary>
        /// <remarks>
        ///     This command provides a list of all languages supported by the external translation API,
        ///     helping users to configure their translation and auto-translation settings correctly.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Translangs()
        {
            await ctx.Channel.SendTableAsync(google.Languages, str => $"{str,-15}").ConfigureAwait(false);
        }
    }
}