using System;
using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Modules.Suggestions.Services;
using Mewdeko.Services;
using Mewdeko.Services.strings;

namespace Mewdeko.Common
{
    public abstract class MewdekoModule : ModuleBase
    {
        protected CultureInfo CultureInfo { get; set; }
        public IBotStrings Strings { get; set; }
        public CommandHandler CmdHandler { get; set; }
        public ILocalization Localization { get; set; }
        public SuggestionsService SugServ { get; set; }
        public UserPunishService UPun { get; set; }
        public ServerManagementService SMS { get; set; }
        public UserPunishService2 UPun2 { get; set; }
        public MuteService MServ { get; set; }

        public string Prefix => CmdHandler.GetPrefix(ctx.Guild);
        public IRole MuteRole => MServ.GetMuteRole(ctx.Guild).Result;
        public ulong WarnlogChannel => UPun.GetWarnlogChannel(ctx.Guild.Id);
        public ulong TTicketCategory => SMS.GetTicketCategory(ctx.Guild.Id);
        public ulong MWarnlogChannel => UPun2.GetMWarnlogChannel(ctx.Guild.Id);
        public ulong SuggestChannel => SugServ.GetSuggestionChannel(ctx.Guild.Id);


        protected ICommandContext ctx => Context;

        protected override void BeforeExecute(CommandInfo cmd)
        {
            CultureInfo = Localization.GetCultureInfo(ctx.Guild?.Id);
        }
        protected string GetText(string key)
        {
            return Strings.GetText(key, CultureInfo);
        }

        protected string GetText(string key, params object[] args)
        {
            return Strings.GetText(key, CultureInfo, args);
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

        public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid)
        {
            embed.WithOkColor();
            var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success).WithButton("No", "no", ButtonStyle.Danger);
            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), component: buttons.Build()).ConfigureAwait(false);
            try
            {
                var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);

                if (input != "Yes") return false;

                return true;
            }
            finally
            {
                var _ = Task.Run(() => msg.DeleteAsync());
            }
        }

        public async Task<string> GetButtonInputAsync(ulong channelId, ulong msgId, ulong userId)
        {
            var userInputTask = new TaskCompletionSource<string>();
            var dsc = (DiscordSocketClient)ctx.Client;
            try
            {
                dsc.InteractionCreated += Interaction;
                if ((await Task.WhenAny(userInputTask.Task, Task.Delay(30000)).ConfigureAwait(false)) != userInputTask.Task)
                {
                    return null;
                }
                return await userInputTask.Task.ConfigureAwait(false);
            }
            finally
            {
                dsc.InteractionCreated -= Interaction;
            }
            Task Interaction(SocketInteraction arg)
            {
                if (arg is SocketMessageComponent c)
                    Task.Run(() =>
                    {
                        if (c.Channel.Id != channelId || c.Message.Id != msgId || c.User.Id != userId) { c.DeferAsync(); return Task.CompletedTask; }
                        if (c.Data.CustomId == "yes")
                        {
                            c.DeferAsync();
                            userInputTask.TrySetResult("Yes");
                            return Task.CompletedTask;
                        }
                        c.DeferAsync();
                        userInputTask.TrySetResult(c.Data.CustomId);
                        return Task.CompletedTask;
                    });
                return Task.CompletedTask;
            }
        }
        public async Task<string> NextMessageAsync(ulong channelId, ulong userId)
        {
            var userInputTask = new TaskCompletionSource<string>();
            var dsc = (DiscordSocketClient)ctx.Client;
            try
            {
                dsc.MessageReceived += Interaction;
                if ((await Task.WhenAny(userInputTask.Task, Task.Delay(60000)).ConfigureAwait(false)) != userInputTask.Task)
                {
                    return null;
                }
                return await userInputTask.Task.ConfigureAwait(false);
            }
            finally
            {
                dsc.MessageReceived -= Interaction;
            }
            Task Interaction(SocketMessage arg)
            { 
                Task.Run(() =>
                {
                    if (arg.Author.Id != userId || arg.Channel.Id != channelId) return Task.CompletedTask;
                        userInputTask.TrySetResult(arg.Content);
                        try
                        {
                            arg.DeleteAsync();
                        }
                        catch
                        {
                            //Exclude
                        }
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