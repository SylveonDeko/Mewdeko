using Discord;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Chat_Triggers.Services;
using System.Net.Http;

namespace Mewdeko.Modules.Chat_Triggers;

public class ChatTriggers : MewdekoModuleBase<ChatTriggersService>
{
    public enum All
    {
        All
    }

    private readonly IHttpClientFactory _clientFactory;
    private readonly InteractiveService _interactivity;

    public ChatTriggers(IHttpClientFactory clientFactory, InteractiveService serv)
    {
        _interactivity = serv;
        _clientFactory = clientFactory;
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtsExport()
    {
        _ = ctx.Channel.TriggerTypingAsync();

        var serialized = Service.ExportCrs(ctx.Guild?.Id);
        await using var stream = await serialized.ToStream();
        await ctx.Channel.SendFileAsync(stream, "crs-export.yml");
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtsImport([Remainder] string? input = null)
    {
        input = input?.Trim();

        _ = ctx.Channel.TriggerTypingAsync();

        if (input is null)
        {
            var attachment = ctx.Message.Attachments.FirstOrDefault();
            if (attachment is null)
            {
                await ReplyErrorLocalizedAsync("expr_import_no_input");
                return;
            }

            using var client = _clientFactory.CreateClient();
            input = await client.GetStringAsync(attachment.Url);

            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyErrorLocalizedAsync("expr_import_no_input");
                return;
            }
        }

        if (ctx.Message.Attachments.Count == 0)
        {
            using var client = _clientFactory.CreateClient();
            input = await client.GetStringAsync(input);
        }
        var succ = await Service.ImportCrsAsync((ctx.User as IGuildUser), input);
        if (!succ)
        {
            await ReplyErrorLocalizedAsync("expr_import_invalid_data");
            return;
        }

        await ctx.OkAsync();
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task AddChatTrigger(string key, [Remainder] string message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
            return;

        var cr = await Service.AddAsync(ctx.Guild?.Id, key, message, false);

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("new_chat_trig")));
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task AddChatTriggerRegex(string key, [Remainder] string message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
            return;

        var cr = await Service.AddAsync(ctx.Guild?.Id, key, message, true);

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("new_chat_trig")));
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task EditChatTrigger(int id, [Remainder] string message)
    {
        if (string.IsNullOrWhiteSpace(message) || id < 0)
            return;

        var cr = await Service.EditAsync(ctx.Guild?.Id, id, message, null).ConfigureAwait(false);
        if (cr != null)
            await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("edited_chat_trig"))).ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("edit_fail").ConfigureAwait(false);
    }

    [Cmd, Aliases, Priority(1), ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task ListChatTriggers()
    {
        var chatTriggers = Service.GetChatTriggersFor(ctx.Guild?.Id);

        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(chatTriggers.Length / 20)
                        .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                        .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(GetText("chat_triggers"))
                                                    .WithDescription(string.Join("\n",
                                                        chatTriggers.OrderBy(cr => cr.Trigger).Skip(page * 20)
                                                                       .Take(20).Select(cr =>
                                                                       {
                                                                           var str = $"`#{cr.Id}` {cr.Trigger}";
                                                                           if (cr.AutoDeleteTrigger) str = $"🗑{str}";
                                                                           if (cr.DmResponse) str = $"📪{str}";
                                                                           var reactions = cr.GetReactions();
                                                                           if (reactions.Length > 0)
                                                                           {
                                                                               str =
                                                                                   $"{str} // {string.Join(" ", reactions)}";
                                                                           }

                                                                           return str;
                                                                       })));
        }
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task ListChatTriggersGroup()
    {
        var chatTriggers = Service.GetChatTriggersFor(ctx.Guild?.Id);

        if (chatTriggers == null || chatTriggers.Length == 0)
        {
            await ReplyErrorLocalizedAsync("no_found").ConfigureAwait(false);
        }
        else
        {
            var ordered = chatTriggers
                .GroupBy(cr => cr.Trigger)
                .OrderBy(cr => cr.Key)
                .ToList();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(chatTriggers.Length / 20)
                .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(GetText("name"))
                                                        .WithDescription(string.Join("\r\n",
                                                            ordered.Skip(page * 20).Take(20).Select(cr =>
                                                                $"**{cr.Key.Trim().ToLowerInvariant()}** `x{cr.Count()}`")));
            }
        }
    }

    [Cmd, Aliases]
    public async Task ShowChatTrigger(int id)
    {
        var found = Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (found == null)
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        else
            await ctx.Channel.EmbedAsync(Service.GetEmbed(found, ctx.Guild?.Id)).ConfigureAwait(false);
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task DeleteChatTrigger(int id)
    {
        var ct = await Service.DeleteAsync(ctx.Guild?.Id, id);

        if (ct != null)
            await ctx.Channel.EmbedAsync(Service.GetEmbed(ct, ctx.Guild?.Id), GetText("deleted")).ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtReact(int id, params string[] emojiStrs)
    {
        var cr = Service.GetChatTriggers(Context.Guild?.Id, id);
        if (cr is null)
        {
            await ReplyErrorLocalizedAsync("no_found").ConfigureAwait(false);
            return;
        }

        if (emojiStrs.Length == 0)
        {
            await Service.ResetCrReactions(ctx.Guild?.Id, id);
            await ReplyConfirmLocalizedAsync("ctr_reset", Format.Bold(id.ToString())).ConfigureAwait(false);
            return;
        }

        var succ = new List<string>();
        foreach (var emojiStr in emojiStrs)
        {
            var emote = emojiStr.ToIEmote();

            // i should try adding these emojis right away to the message, to make sure the bot can react with these emojis. If it fails, skip that emoji
            try
            {
                await Context.Message.AddReactionAsync(emote).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);
                succ.Add(emojiStr);

                if (succ.Count >= 6)
                    break;
            }
            catch
            {
                // ignored
            }
        }

        if (succ.Count == 0)
        {
            await ReplyErrorLocalizedAsync("invalid_emojis").ConfigureAwait(false);
            return;
        }

        await Service.SetCrReactions(ctx.Guild?.Id, id, succ);

        await ReplyConfirmLocalizedAsync("ctr_set", Format.Bold(id.ToString()),
            string.Join(", ", succ.Select(x => x.ToString()))).ConfigureAwait(false);
    }

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task CtCa(int id) => InternalCtEdit(id, ChatTriggersService.CtField.ContainsAnywhere);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task Rtt(int id) => InternalCtEdit(id, ChatTriggersService.CtField.ReactToTrigger);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task CtDm(int id) => InternalCtEdit(id, ChatTriggersService.CtField.DmResponse);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task CtAd(int id) => InternalCtEdit(id, ChatTriggersService.CtField.AutoDelete);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task CtAt(int id) => InternalCtEdit(id, ChatTriggersService.CtField.AllowTarget);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public Task CtNr(int id) => InternalCtEdit(id, ChatTriggersService.CtField.NoRespond);

    [Cmd, Aliases, ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task ChatTriggerRoleGrantType(int id, CTRoleGrantType type)
    {
        var res = await Service.SetRoleGrantType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res?.Id != id)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")));
        }
    }

    [Cmd, Aliases, OwnerOnly]
    public async Task CtsReload()
    {
        await Service.TriggerReloadChatTriggers();

        await ctx.OkAsync();
    }

    private async Task InternalCtEdit(int id, ChatTriggersService.CtField option)
    {
        var ct = Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct?.Id != id)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }
        var (success, newVal) = await Service.ToggleCrOptionAsync(ct, option).ConfigureAwait(false);
        if (!success)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        if (newVal)
        {
            await ReplyConfirmLocalizedAsync("option_enabled", Format.Code(option.ToString()),
                        Format.Code(id.ToString())).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("option_disabled", Format.Code(option.ToString()),
                        Format.Code(id.ToString())).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
    ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtsClear()
    {
        if (await PromptUserConfirmAsync(new EmbedBuilder()
                    .WithTitle("Chat triggers clear")
                    .WithDescription("This will delete all chat triggers on this server."), ctx.User.Id)
                .ConfigureAwait(false))
        {
            var count = Service.DeleteAllChatTriggers(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("cleared", count).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
        ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtrGrantToggle(int id, IRole role)
    {
        var gUsr = ctx.User as IGuildUser;

        if (!role.CanManageRole(gUsr))
        {
            await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
            return;
        }

        var cr = Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (cr is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        if (cr.GetRemovedRoles().Contains(role.Id))
        {
            await ReplyErrorLocalizedAsync("ct_roll_add_remove").ConfigureAwait(false);
            return;
        }

        await Service.ToggleGrantedRole(cr, role.Id).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("ct_toggled_roll_grant", Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
        ChatTriggerPermCheck(GuildPermission.Administrator)]
    public async Task CtrRemoveToggle(int id, IRole role)
    {
        var gUsr = ctx.User as IGuildUser;

        if (!role.CanManageRole(gUsr))
        {
            await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
            return;
        }

        var cr = Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (cr is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        if (cr.GetGrantedRoles().Contains(role.Id))
        {
            await ReplyErrorLocalizedAsync("ct_roll_add_remove").ConfigureAwait(false);
            return;
        }

        await Service.ToggleRemovedRole(cr, role.Id).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("ct_toggled_roll_remove", Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);
    }
}
