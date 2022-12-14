using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class TranslateCommands : MewdekoSubmodule
    {
        public enum AutoDeleteAutoTranslate
        {
            Del,
            Nodel
        }

        private readonly IGoogleApiService google;
        private readonly SearchesService searches;

        public TranslateCommands(SearchesService searches, IGoogleApiService google)
        {
            this.searches = searches;
            this.google = google;
        }

        [Cmd, Aliases]
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

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Translangs() => await ctx.Channel.SendTableAsync(google.Languages, str => $"{str,-15}").ConfigureAwait(false);
    }
}