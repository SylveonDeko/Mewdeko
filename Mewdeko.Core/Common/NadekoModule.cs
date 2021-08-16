using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.ServerManagement.Services;
using Mewdeko.Modules.Suggestions.Services;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules
{
    public abstract class MewdekoModule : ModuleBase
    {
        protected CultureInfo _cultureInfo { get; set; }
        public IBotStrings Strings { get; set; }
        public CommandHandler CmdHandler { get; set; }
        public ILocalization Localization { get; set; }
        public SuggestService SugServ { get; set; }
        public UserPunishService UPun { get; set; }
        public ServerManagementService SMS { get; set; }
        public UserPunishService2 UPun2 { get; set; }

        public string Prefix => CmdHandler.GetPrefix(ctx.Guild);
        public ulong WarnlogChannel => UPun.GetWarnlogChannel(ctx.Guild.Id);
        public ulong TTicketCategory => SMS.GetTicketCategory(ctx.Guild.Id);
        public ulong MWarnlogChannel => UPun2.GetMWarnlogChannel(ctx.Guild.Id);
        public ulong SuggestChannel => SugServ.GetSuggestionChannel(ctx.Guild.Id);
        public ulong sugnum => SugServ.GetSNum(ctx.Guild.Id);


        protected ICommandContext ctx => Context;

        protected override void BeforeExecute(CommandInfo cmd)
        {
            _cultureInfo = Localization.GetCultureInfo(ctx.Guild?.Id);
        }

        protected string GetText(string key)
        {
            return Strings.GetText(key, _cultureInfo);
        }

        protected string GetText(string key, params object[] args)
        {
            return Strings.GetText(key, _cultureInfo, args);
        }

        public Task<IUserMessage> ErrorLocalizedAsync(string textKey, params object[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendErrorAsync(text);
        }

        public Task<IUserMessage> ReplyErrorLocalizedAsync(string textKey, params object[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendErrorAsync(Format.Bold(ctx.User.ToString()) + " " + text);
        }

        public Task<IUserMessage> ConfirmLocalizedAsync(string textKey, params object[] args)
        {
            var text = GetText(textKey, args);
            return ctx.Channel.SendConfirmAsync(text);
        }

        public Task<IUserMessage> ReplyConfirmLocalizedAsync(string textKey, params object[] args)
        {
            var text = GetText(textKey, args);
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

                if (input != "YES" && input != "Y") return false;

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
            var dsc = (DiscordSocketClient) ctx.Client;
            try
            {
                dsc.MessageReceived += MessageReceived;

                if (await Task.WhenAny(userInputTask.Task, Task.Delay(10000)).ConfigureAwait(false) !=
                    userInputTask.Task) return null;

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
                        return Task.CompletedTask;

                    if (userInputTask.TrySetResult(arg.Content)) userMsg.DeleteAfter(1);
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            }
        }
    }

    public abstract class MewdekoModule<TService> : MewdekoModule
    {
        public TService _service { get; set; }
    }

    public abstract class MewdekoSubmodule : MewdekoModule
    {
    }

    public abstract class MewdekoSubmodule<TService> : MewdekoModule<TService>
    {
    }
}