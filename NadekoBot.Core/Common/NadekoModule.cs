using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Extensions;
using NLog;
using System.Globalization;
using System.Threading.Tasks;

namespace NadekoBot.Modules
{
    public abstract class NadekoTopLevelModule : ModuleBase
    {
        protected Logger _log { get; }
        protected CultureInfo _cultureInfo { get; set; }
        public IBotStrings Strings { get; set; }
        public IBotConfigProvider Bc { get; set; }
        public CommandHandler CmdHandler { get; set; }
        public ILocalization Localization { get; set; }

        public string Prefix => CmdHandler.GetPrefix(ctx.Guild);

        protected ICommandContext ctx => Context;

        protected NadekoTopLevelModule(bool isTopLevelModule = true)
        {
            //if it's top level module
            _log = LogManager.GetCurrentClassLogger();
        }

        protected override void BeforeExecute(CommandInfo cmd)
        {
            _cultureInfo = Localization.GetCultureInfo(ctx.Guild?.Id);
        }

        //public Task<IUserMessage> ReplyConfirmLocalized(string titleKey, string textKey, string url = null, string footer = null)
        //{
        //    var title = NadekoBot.ResponsesResourceManager.GetString(titleKey, cultureInfo);
        //    var text = NadekoBot.ResponsesResourceManager.GetString(textKey, cultureInfo);
        //    return ctx.Channel.SendConfirmAsync(title, text, url, footer);
        //}

        //public Task<IUserMessage> ReplyConfirmLocalized(string textKey)
        //{
        //    var text = NadekoBot.ResponsesResourceManager.GetString(textKey, cultureInfo);
        //    return ctx.Channel.SendConfirmAsync(ctx.User.Mention + " " + textKey);
        //}

        //public Task<IUserMessage> ReplyErrorLocalized(string titleKey, string textKey, string url = null, string footer = null)
        //{
        //    var title = NadekoBot.ResponsesResourceManager.GetString(titleKey, cultureInfo);
        //    var text = NadekoBot.ResponsesResourceManager.GetString(textKey, cultureInfo);
        //    return ctx.Channel.SendErrorAsync(title, text, url, footer);
        //}

        protected string GetText(string key) =>
            Strings.GetText(key, _cultureInfo);

        protected string GetText(string key, params object[] replacements) =>
            Strings.GetText(key, _cultureInfo, replacements);

        public Task<IUserMessage> ErrorLocalizedAsync(string textKey, params object[] replacements)
        {
            var text = GetText(textKey, replacements);
            return ctx.Channel.SendErrorAsync(text);
        }

        public Task<IUserMessage> ReplyErrorLocalizedAsync(string textKey, params object[] replacements)
        {
            var text = GetText(textKey, replacements);
            return ctx.Channel.SendErrorAsync(Format.Bold(ctx.User.ToString()) + " " + text);
        }

        public Task<IUserMessage> ConfirmLocalizedAsync(string textKey, params object[] replacements)
        {
            var text = GetText(textKey, replacements);
            return ctx.Channel.SendConfirmAsync(text);
        }

        public Task<IUserMessage> ReplyConfirmLocalizedAsync(string textKey, params object[] replacements)
        {
            var text = GetText(textKey, replacements);
            return ctx.Channel.SendConfirmAsync(Format.Bold(ctx.User.ToString()) + " " + text);
        }

        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed)
        {
            embed.WithOkColor()
                .WithFooter("yes/no");

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            try
            {
                var input = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id).ConfigureAwait(false);
                input = input?.ToUpperInvariant();

                if (input != "YES" && input != "Y")
                {
                    return false;
                }

                return true;
            }
            finally
            {
                var _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        // TypeConverter typeConverter = TypeDescriptor.GetConverter(propType); ?
        public async Task<string> GetUserInputAsync(ulong userId, ulong channelId)
        {
            var userInputTask = new TaskCompletionSource<string>();
            var dsc = (DiscordSocketClient)ctx.Client;
            try
            {
                dsc.MessageReceived += MessageReceived;

                if ((await Task.WhenAny(userInputTask.Task, Task.Delay(10000)).ConfigureAwait(false)) != userInputTask.Task)
                {
                    return null;
                }

                return await userInputTask.Task.ConfigureAwait(false);
            }
            finally
            {
                dsc.MessageReceived -= MessageReceived;
            }

            Task MessageReceived(SocketMessage arg)
            {
                var _ = Task.Run(() =>
                {
                    if (!(arg is SocketUserMessage userMsg) ||
                        !(userMsg.Channel is ITextChannel chan) ||
                        userMsg.Author.Id != userId ||
                        userMsg.Channel.Id != channelId)
                    {
                        return Task.CompletedTask;
                    }

                    if (userInputTask.TrySetResult(arg.Content))
                    {
                        userMsg.DeleteAfter(1);
                    }
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            }
        }
    }

    public abstract class NadekoTopLevelModule<TService> : NadekoTopLevelModule where TService : INService
    {
        public TService _service { get; set; }

        protected NadekoTopLevelModule(bool isTopLevel = true) : base(isTopLevel)
        {
        }
    }

    public abstract class NadekoSubmodule : NadekoTopLevelModule
    {
        protected NadekoSubmodule() : base(false) { }
    }

    public abstract class NadekoSubmodule<TService> : NadekoTopLevelModule<TService> where TService : INService
    {
        protected NadekoSubmodule() : base(false)
        {
        }
    }
}