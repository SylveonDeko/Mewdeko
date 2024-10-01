using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Chat_Triggers.Services;

namespace Mewdeko.Modules.Chat_Triggers;

/// <summary>
///     Module for chat triggers.
/// </summary>
/// <param name="clientFactory"></param>
/// <param name="serv"></param>
public class ChatTriggers(IHttpClientFactory clientFactory, InteractiveService serv)
    : MewdekoModuleBase<ChatTriggersService>
{
    /// <summary>
    ///     Exports chat trigger settings for the current guild.
    /// </summary>
    /// <example>.ctsexport</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtsExport()
    {
        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var serialized = await Service.ExportCrs(ctx.Guild?.Id);
        var stream = await serialized.ToStream().ConfigureAwait(false);
        await using var a = stream.ConfigureAwait(false);
        await ctx.Channel.SendFileAsync(stream, "crs-export.yml").ConfigureAwait(false);
    }

    /// <summary>
    ///     Imports chat trigger settings for the current guild.
    /// </summary>
    /// <param name="input">The input containing the custom reaction settings.</param>
    /// <example>.ctsimport url</example>
    /// <example>.ctsimport attachment</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtsImport([Remainder] string? input = null)
    {
        input = input?.Trim();

        _ = ctx.Channel.TriggerTypingAsync();

        if (input is null)
        {
            var attachment = ctx.Message.Attachments.FirstOrDefault();
            if (attachment is null)
            {
                await ReplyErrorLocalizedAsync("expr_import_no_input").ConfigureAwait(false);
                return;
            }

            using var client = clientFactory.CreateClient();
            input = await client.GetStringAsync(attachment.Url).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyErrorLocalizedAsync("expr_import_no_input").ConfigureAwait(false);
                return;
            }
        }

        if (ctx.Message.Attachments.Count == 0)
        {
            using var client = clientFactory.CreateClient();
            input = await client.GetStringAsync(input).ConfigureAwait(false);
        }

        var succ = await Service.ImportCrsAsync(ctx.User as IGuildUser, input).ConfigureAwait(false);
        if (!succ)
        {
            await ReplyErrorLocalizedAsync("expr_import_invalid_data").ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }


    /// <summary>
    ///     Adds a new chat trigger.
    /// </summary>
    /// <param name="key">The key for the chat trigger.</param>
    /// <param name="message">The message associated with the chat trigger.</param>
    /// <example>.act trigger response</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AddChatTrigger(string key, [Remainder] string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
            return;

        var cr = await Service.AddAsync(ctx.Guild?.Id, key, message, false).ConfigureAwait(false);

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("new_chat_trig")))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new chat trigger with regex support.
    /// </summary>
    /// <param name="key">The key for the chat trigger.</param>
    /// <param name="message">The message associated with the chat trigger.</param>
    /// <example>.actr trigger* response</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AddChatTriggerRegex(string key, [Remainder] string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(key))
            return;

        var cr = await Service.AddAsync(ctx.Guild?.Id, key, message, true).ConfigureAwait(false);

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("new_chat_trig")))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Edits an existing chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <param name="message">The new message for the chat trigger.</param>
    /// <example>.ect 9987 Response</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task EditChatTrigger(int id, [Remainder] string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || id < 0)
            return;

        var cr = await Service.EditAsync(ctx.Guild?.Id, id, message, null).ConfigureAwait(false);
        if (cr != null)
            await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("edit_fail").ConfigureAwait(false);
    }


    /// <summary>
    ///     Lists all chat triggers.
    /// </summary>
    /// <example>.lct</example>
    [Cmd]
    [Aliases]
    [Priority(1)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ListChatTriggers()
    {
        var chatTriggers = await Service.GetChatTriggersFor(ctx.Guild?.Id);

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(chatTriggers.Length / 20).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(GetText("chat_triggers")).WithDescription(
                string.Join("\n", chatTriggers.OrderBy(cr => cr.Trigger).Skip(page * 20).Take(20).Select(cr =>
                {
                    var str = $"`#{cr.Id}` {cr.Trigger}";
                    if (cr.AutoDeleteTrigger)
                        str = $"🗑{str}";
                    if (cr.DmResponse)
                        str = $"📪{str}";
                    var reactions = cr.GetReactions();
                    if (reactions.Length > 0)
                    {
                        str = $"{str} // {string.Join(" ", reactions)}";
                    }

                    return str;
                })));
        }
    }

    /// <summary>
    ///     Lists all chat triggers grouped by trigger.
    /// </summary>
    /// <example>.lctg</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ListChatTriggersGroup()
    {
        var chatTriggers = await Service.GetChatTriggersFor(ctx.Guild?.Id);

        if (!chatTriggers.Any())
        {
            await ReplyErrorLocalizedAsync("no_found").ConfigureAwait(false);
        }
        else
        {
            var ordered = chatTriggers.GroupBy(cr => cr.Trigger).OrderBy(cr => cr.Key).ToList();

            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(chatTriggers.Length / 20).WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(GetText("name")).WithDescription(
                    string.Join("\r\n",
                        ordered.Skip(page * 20).Take(20).Select(cr =>
                            $"**{cr.Key.Trim().ToLowerInvariant()}** `x{cr.Count()}`")));
            }
        }
    }

    /// <summary>
    ///     Shows details of a specific chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <example>.sct 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ShowChatTrigger(int id)
    {
        var found = await Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (found == null)
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        else
            await ctx.Channel.EmbedAsync(Service.GetEmbed(found, ctx.Guild?.Id)).ConfigureAwait(false);
    }


    /// <summary>
    ///     Deletes a chat trigger by its ID.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to delete.</param>
    /// <example>.dct 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task DeleteChatTrigger(int id)
    {
        var ct = await Service.DeleteAsync(ctx.Guild?.Id, id).ConfigureAwait(false);

        if (ct != null)
            await ctx.Channel.EmbedAsync(Service.GetEmbed(ct, ctx.Guild?.Id), GetText("deleted")).ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets reactions for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="emojiStrs">The emoji strings to set as reactions.</param>
    /// <example>.ctr 9987 :sylvhappy: :sylvissadthathehastomakedocs:</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtReact(int id, params string[] emojiStrs)
    {
        var cr = await Service.GetChatTriggers(Context.Guild?.Id, id);
        if (cr is null)
        {
            await ReplyErrorLocalizedAsync("no_found").ConfigureAwait(false);
            return;
        }

        if (emojiStrs.Length == 0)
        {
            await Service.ResetCrReactions(ctx.Guild?.Id, id).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("ctr_reset", Format.Bold(id.ToString())).ConfigureAwait(false);
            return;
        }

        var succ = new List<string>();
        foreach (var emojiStr in emojiStrs)
        {
            var emote = emojiStr.ToIEmote();

            // Try adding these emojis right away to the message, to make sure the bot can react with these emojis. If it fails, skip that emoji.
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
                // Ignored
            }
        }

        if (succ.Count == 0)
        {
            await ReplyErrorLocalizedAsync("invalid_emojis").ConfigureAwait(false);
            return;
        }

        await Service.SetCrReactions(ctx.Guild?.Id, id, succ).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("ctr_set", Format.Bold(id.ToString()),
            string.Join(", ", succ.Select(x => x.ToString()))).ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets a chat trigger to contain anywhere in the message.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.ctca 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtCa(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.ContainsAnywhere);
    }

    /// <summary>
    ///     Sets a chat trigger to react to the trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.rtt 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task Rtt(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.ReactToTrigger);
    }

    /// <summary>
    ///     Sets a chat trigger to send a direct message in response.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.ctdm 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtDm(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.DmResponse);
    }

    /// <summary>
    ///     Sets a chat trigger to auto-delete after triggering.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.ctad 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtAd(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.AutoDelete);
    }

    /// <summary>
    ///     Sets a chat trigger to allow targeting.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.ctat 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtAt(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.AllowTarget);
    }

    /// <summary>
    ///     Sets a chat trigger to not respond.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <example>.ctnr 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtNr(int id)
    {
        return InternalCtEdit(id, ChatTriggersService.CtField.NoRespond);
    }


    /// <summary>
    ///     Sets the role grant type for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="type">The role grant type to set. <see cref="CtRoleGrantType" /></param>
    /// <example>.ctrgt 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ChatTriggerRoleGrantType(int id, CtRoleGrantType type)
    {
        var res = await Service.SetRoleGrantType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res?.Id != id)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reloads chat triggers.
    /// </summary>
    /// <example>.ctsreload</example>
    [Cmd]
    [Aliases]
    [OwnerOnly]
    public async Task CtsReload()
    {
        await Service.TriggerReloadChatTriggers().ConfigureAwait(false);

        await ctx.OkAsync().ConfigureAwait(false);
    }


    /// <summary>
    ///     Toggles a chat trigger option.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="option">The option to toggle.</param>
    private async Task InternalCtEdit(int id, ChatTriggersService.CtField option)
    {
        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
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

    /// <summary>
    ///     Clears all chat triggers.
    /// </summary>
    /// <example>.ctsclear</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtsClear()
    {
        if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithTitle(GetText("ct_clear"))
                        .WithDescription(GetText("ct_clear_done")),
                    ctx.User.Id)
                .ConfigureAwait(false))
        {
            var count = Service.DeleteAllChatTriggers(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("cleared", count).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles role grant for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="role">The role to toggle.</param>
    /// <example>.ctgt 9987 @role</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtrGrantToggle(int id, IRole role)
    {
        var gUsr = ctx.User as IGuildUser;

        if (!role.CanManageRole(gUsr))
        {
            await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
            return;
        }

        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (ct is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        var toggleDisabled = ct.IsToggled(role.Id);

        await Service.ToggleGrantedRole(ct, role.Id).ConfigureAwait(false);

        var str = toggleDisabled ? "ct_role_toggle_disabled" :
            ct.IsToggled(role.Id) ? "ct_role_toggle_enabled" :
            ct.IsGranted(role.Id) ? "ct_role_add_enabled" : "ct_role_add_disabled";

        await ReplyConfirmLocalizedAsync(str, Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles role removal for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="role">The role to toggle.</param>
    /// <example>.ctrt 9987 @role</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtrRemoveToggle(int id, IRole role)
    {
        var gUsr = ctx.User as IGuildUser;

        if (!role.CanManageRole(gUsr))
        {
            await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
            return;
        }

        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        var toggleDisabled = ct.IsToggled(role.Id);

        await Service.ToggleRemovedRole(ct, role.Id).ConfigureAwait(false);

        var str = toggleDisabled ? "ct_role_toggle_disabled" :
            ct.IsToggled(role.Id) ? "ct_role_toggle_enabled" :
            ct.IsRemoved(role.Id) ? "ct_role_remove_enabled" : "cr_role_remove_disabled";

        await ReplyConfirmLocalizedAsync(str, Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether a chat trigger is valid for a specific trigger type.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="type">The trigger type to set validity for. <see cref="ChatTriggerType" /></param>
    /// <param name="enabled">Whether the trigger type should be enabled or disabled.</param>
    /// <example>.chattriggervalidtype 9987 Slash true</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ChatTriggerValidType(int id, ChatTriggerType type, bool enabled)
    {
        var res = await Service.SetValidTriggerType(ctx.Guild?.Id, id, type, enabled).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel
                .SendMessageAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the webhook URL for crossposting a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="webhookUrl">The webhook URL to set.</param>
    /// <example>.chattriggerscrosspostwebhook 9987 webhookurl</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCpSetWebhook(int id, string webhookUrl)
    {
        var res = await Service.SetCrosspostingWebhookUrl(ctx.Guild?.Id, id, webhookUrl).ConfigureAwait(false);
        if (!res.Valid)
        {
            await ReplyErrorLocalizedAsync("ct_webhook_invalid").ConfigureAwait(false);
            return;
        }

        if (res.Trigger is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res.Trigger, ctx.Guild?.Id, GetText("edited_chat_trig")))
            .ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the crossposting channel for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="channel">The channel to set for crossposting.</param>
    /// <example>.chattriggerscrosspostchannel 9987 #channel</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCpSetChannel(int id, ITextChannel channel)
    {
        var res = await Service.SetCrosspostingChannelId(ctx.Guild?.Id, id, channel.Id).ConfigureAwait(false);
        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
            .ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets the interaction type for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="type">The interaction type to set. <see cref="CtApplicationCommandType" /></param>
    /// <example>.setctintertype 9987 Slash</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterType(int id, CtApplicationCommandType type)
    {
        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }

        // Validate the name based on type
        if (type != CtApplicationCommandType.None
            && !ChatTriggersService.IsValidName(type,
                string.IsNullOrWhiteSpace(ct.ApplicationCommandName) ? ct.Trigger : ct.ApplicationCommandName))
        {
            await ReplyErrorLocalizedAsync("ct_interaction_name_invalid").ConfigureAwait(false);
            return;
        }

        var res = await Service.SetInteractionType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the interaction name for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="name">The name to set for the interaction.</param>
    /// <example>.setctintername 9987 ihatedocumentation</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterName(int id, string name)
    {
        var res = await Service.SetInteractionName(ctx.Guild?.Id, id, name).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the interaction description for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="description">The description to set for the interaction.</param>
    /// <example>.setctinterdesc 9987 3591 things to continue documenting....</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterDesc(int id, string description)
    {
        var res = await Service.SetInteractionDescription(ctx.Guild?.Id, id, description).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets whether the interaction response should be ephemeral for a chat trigger. To not show others my suffering with
    ///     docs!
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="ephemeral">True if the response should be ephemeral, false otherwise.</param>
    /// <example>.ctca 9987 true/false</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtInterEphemeral(int id, bool ephemeral)
    {
        var res = await Service.SetInteractionEphemeral(ctx.Guild?.Id, id, ephemeral).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the interaction errors for chat triggers.
    /// </summary>
    /// <example>.ctintererrors</example>
    [Cmd]
    [Alias]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtInterErrors()
    {
        var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
        var eb = new EmbedBuilder();
        var cb = new ComponentBuilder().WithButton("Support Server", style: ButtonStyle.Link,
            url: "https://discord.gg/Mewdeko",
            emote: Emote.Parse("<:IconInvite:778931752835088426>"));
        if (errors?.Any() ?? false)
        {
            eb.WithFields(errors.Select(x =>
                    new EmbedFieldBuilder().WithName(GetText($"ct_interr_{x.ErrorKey}")).WithValue(
                        GetText($"ct_interr_{x.ErrorKey}_body", x.CtRealNames.Select(s => $" - {s}").Join('\n')))))
                .WithTitle(GetText("ct_interaction_errors_info_title", errors.Count))
                .WithDescription(GetText("ct_interaction_errors_info_desc")).WithErrorColor();
        }
        else
        {
            eb.WithOkColor().WithTitle(GetText("ct_interaction_errors_none"))
                .WithDescription(GetText("ct_interaction_errors_none_desc"));
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: cb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the prefix type for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="type">The type of prefix to set. <see cref="RequirePrefixType" /></param>
    /// <example>.ctprefixtype 9987 Guild</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtPrefixType(int id, RequirePrefixType type)
    {
        var res = await Service.SetPrefixType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the prefix for a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="prefix">The new prefix to set.</param>
    /// <example>
    ///     .ctprefix 123 !
    /// </example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtPrefix(int id, string prefix)
    {
        var res = await Service.SetPrefix(ctx.Guild?.Id, id, prefix).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Checks for any interaction errors related to chat triggers and sends a follow-up message with their status.
    /// </summary>
    public async Task FollowupWithTriggerStatus()
    {
        var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
        if (!(errors?.Any() ?? false))
            return;
        var embed = new EmbedBuilder()
            .WithTitle(GetText("ct_interaction_errors_title"))
            .WithDescription(GetText("ct_interaction_errors_desc"))
            .WithErrorColor();
        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }
}