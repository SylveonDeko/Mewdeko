using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class TranslateCommands : MewdekoSubmodule
        {
            //[MewdekoCommand, Usage, Description, Aliases]
            //[OwnerOnly]
            //public async Task Obfuscate([Leftover] string txt)
            //{
            //    var lastItem = "en";
            //    foreach (var item in _google.Languages.Except(new[] { "en" }).Where(x => x.Length < 4))
            //    {
            //        var txt2 = await _searches.Translate(lastItem + ">" + item, txt);
            //        await ctx.Channel.EmbedAsync(new EmbedBuilder()
            //            .WithOkColor()
            //            .WithTitle(lastItem + ">" + item)
            //            .AddField("Input", txt)
            //            .AddField("Output", txt2));
            //        txt = txt2;
            //        await Task.Delay(500);
            //        lastItem = item;
            //    }
            //    txt = await _searches.Translate(lastItem + ">en", txt);
            //    await ctx.Channel.SendConfirmAsync("Final output:\n\n" + txt);
            //}

            public enum AutoDeleteAutoTranslate
            {
                Del,
                Nodel
            }

            private readonly IGoogleApiService _google;
            private readonly SearchesService _searches;

            public TranslateCommands(SearchesService searches, IGoogleApiService google)
            {
                _searches = searches;
                _google = google;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Translate(string langs, [Leftover] string text = null)
            {
                try
                {
                    await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    var translation = await _searches.Translate(langs, text).ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(GetText("translation") + " " + langs, translation)
                        .ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("bad_input_format").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [OwnerOnly]
            public async Task AutoTranslate(AutoDeleteAutoTranslate autoDelete = AutoDeleteAutoTranslate.Nodel)
            {
                var channel = (ITextChannel) ctx.Channel;

                if (autoDelete == AutoDeleteAutoTranslate.Del)
                {
                    _searches.TranslatedChannels.AddOrUpdate(channel.Id, true, (key, val) => true);
                    await ReplyConfirmLocalizedAsync("atl_ad_started").ConfigureAwait(false);
                    return;
                }

                if (_searches.TranslatedChannels.TryRemove(channel.Id, out _))
                {
                    await ReplyConfirmLocalizedAsync("atl_stopped").ConfigureAwait(false);
                    return;
                }

                if (_searches.TranslatedChannels.TryAdd(channel.Id, autoDelete == AutoDeleteAutoTranslate.Del))
                    await ReplyConfirmLocalizedAsync("atl_started").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AutoTransLang([Leftover] string langs = null)
            {
                var ucp = (ctx.User.Id, ctx.Channel.Id);

                if (string.IsNullOrWhiteSpace(langs))
                {
                    if (_searches.UserLanguages.TryRemove(ucp, out langs))
                        await ReplyConfirmLocalizedAsync("atl_removed").ConfigureAwait(false);
                    return;
                }

                var langarr = langs.ToLowerInvariant().Split('>');
                if (langarr.Length != 2)
                    return;
                var from = langarr[0];
                var to = langarr[1];

                if (!_google.Languages.Contains(from) || !_google.Languages.Contains(to))
                {
                    await ReplyErrorLocalizedAsync("invalid_lang").ConfigureAwait(false);
                    return;
                }

                _searches.UserLanguages.AddOrUpdate(ucp, langs, (key, val) => langs);

                await ReplyConfirmLocalizedAsync("atl_set", from, to).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Translangs()
            {
                await ctx.Channel.SendTableAsync(_google.Languages, str => $"{str,-15}").ConfigureAwait(false);
            }
        }
    }
}