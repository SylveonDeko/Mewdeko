using System.Net.Http;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Chat_Triggers;

/// <summary>
///     Module for chat triggers.
/// </summary>
/// <param name="clientFactory"></param>
/// <param name="serv"></param>
/// <param name="counterService">Store for the counters trigger responses read and update.</param>
public class ChatTriggers(
    IHttpClientFactory clientFactory,
    InteractiveService serv,
    TriggerCounterService counterService)
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
                await ReplyErrorAsync(Strings.ExprImportNoInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            using var client = clientFactory.CreateClient();
            input = await client.GetStringAsync(attachment.Url).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyErrorAsync(Strings.ExprImportNoInput(ctx.Guild.Id)).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.ExprImportInvalidData(ctx.Guild.Id)).ConfigureAwait(false);
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

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, Strings.NewChatTrig(ctx.Guild.Id)))
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

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, Strings.NewChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new reaction-based chat trigger.
    /// </summary>
    /// <param name="reaction">The emoji/emote that will trigger this response.</param>
    /// <param name="message">The message associated with the chat trigger.</param>
    /// <example>.artrig 👍 You reacted with thumbs up!</example>
    /// <example>.artrig :custom_emote: Custom emote reaction!</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AddReactionTrigger(string reaction, [Remainder] string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(reaction))
            return;

        var cr = await Service.AddReactionTriggerAsync(ctx.Guild?.Id, reaction, message).ConfigureAwait(false);

        await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, Strings.NewReactionTrigger(ctx.Guild.Id)))
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
            await ctx.Channel.EmbedAsync(Service.GetEmbed(cr, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
                .ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.EditFail(ctx.Guild.Id)).ConfigureAwait(false);
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
            return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(Strings.ChatTriggers(ctx.Guild.Id))
                .WithDescription(
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
            await ReplyErrorAsync(Strings.NoFound(ctx.Guild.Id)).ConfigureAwait(false);
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
                return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(Strings.Name(ctx.Guild.Id))
                    .WithDescription(
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
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
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
            await ctx.Channel.EmbedAsync(Service.GetEmbed(ct, ctx.Guild?.Id), Strings.Deleted(ctx.Guild.Id))
                .ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.NoFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (emojiStrs.Length == 0)
        {
            await Service.ResetCrReactions(ctx.Guild?.Id, id).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.CtrReset(ctx.Guild.Id, Format.Bold(id.ToString()))).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.InvalidEmojis(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetCrReactions(ctx.Guild?.Id, id, succ).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.CtrSet(ctx.Guild.Id, Format.Bold(id.ToString()),
            string.Join(", ", succ.Select(x => x.ToString())))).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (success, newVal) = await Service.ToggleCrOptionAsync(ct, option).ConfigureAwait(false);
        if (!success)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (newVal)
        {
            await ReplyConfirmAsync(Strings.OptionEnabled(ctx.Guild.Id, Format.Code(option.ToString()),
                Format.Code(id.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.OptionDisabled(ctx.Guild.Id, Format.Code(option.ToString()),
                Format.Code(id.ToString()))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Migrates old CrEmbed format chat triggers to the new embed format.
    ///     First exports a backup, then converts all triggers.
    /// </summary>
    /// <example>.ctsmigrate</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtsMigrate()
    {
        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

        // First export a backup
        var serialized = await Service.ExportCrs(ctx.Guild?.Id);
        var stream = await serialized.ToStream().ConfigureAwait(false);
        await using var _ = stream.ConfigureAwait(false);
        await ctx.Channel.SendFileAsync(stream, $"crs-backup-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.yml",
            Strings.CtMigrateBackup(ctx.Guild.Id)).ConfigureAwait(false);

        // Perform the migration
        var result = await Service.MigrateCrEmbedFormat(ctx.Guild?.Id).ConfigureAwait(false);

        if (result.TotalChecked == 0)
        {
            await ReplyErrorAsync(Strings.CtMigrateNoTriggers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (result.Migrated == 0)
        {
            await ReplyConfirmAsync(Strings.CtMigrateNoneNeeded(ctx.Guild.Id, result.TotalChecked))
                .ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.CtMigrateSuccess(ctx.Guild.Id, result.Migrated, result.TotalChecked))
            .ConfigureAwait(false);
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
                    new EmbedBuilder().WithTitle(Strings.CtClear(ctx.Guild.Id))
                        .WithDescription(Strings.CtClearDone(ctx.Guild.Id)),
                    ctx.User.Id)
                .ConfigureAwait(false))
        {
            var count = Service.DeleteAllChatTriggers(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.Cleared(ctx.Guild.Id, count)).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.CantManageRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var toggleDisabled = ct.IsToggled(role.Id);

        await Service.ToggleGrantedRole(ct, role.Id).ConfigureAwait(false);

        var text = toggleDisabled
            ? Strings.CtRoleToggleDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
            : ct.IsToggled(role.Id)
                ? Strings.CtRoleToggleEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                : ct.IsGranted(role.Id)
                    ? Strings.CtRoleAddEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                    : Strings.CtRoleAddDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()));

        await ReplyConfirmAsync(text).ConfigureAwait(false);
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
            await ReplyErrorAsync(Strings.CantManageRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var toggleDisabled = ct.IsToggled(role.Id);

        await Service.ToggleRemovedRole(ct, role.Id).ConfigureAwait(false);

        var text = toggleDisabled
            ? Strings.CtRoleToggleDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
            : ct.IsToggled(role.Id)
                ? Strings.CtRoleToggleEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                : ct.IsRemoved(role.Id)
                    ? Strings.CtRoleRemoveEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                    : Strings.CtRoleRemoveDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()));

        await ReplyConfirmAsync(text).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether a chat trigger is valid for a specific trigger type.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="type">The trigger type to set validity for. <see cref="ChatTriggerType" /></param>
    /// <param name="enabled">Whether the trigger type should be enabled or disabled.</param>
    /// <example>.chattriggervalidtype 9987 Slash true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ChatTriggerValidType(int id, ChatTriggerType type, bool enabled)
    {
        var res = await Service.SetValidTriggerType(ctx.Guild?.Id, id, type, enabled).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel
                .SendMessageAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                    .Build())
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCpSetWebhook(int id, string webhookUrl)
    {
        var res = await Service.SetCrosspostingWebhookUrl(ctx.Guild?.Id, id, webhookUrl).ConfigureAwait(false);
        if (!res.Valid)
        {
            await ReplyErrorAsync(Strings.CtWebhookInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (res.Trigger is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res.Trigger, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCpSetChannel(int id, ITextChannel channel)
    {
        var res = await Service.SetCrosspostingChannelId(ctx.Guild?.Id, id, channel.Id).ConfigureAwait(false);
        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterType(int id, CtApplicationCommandType type)
    {
        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Validate the name based on type
        if (type != CtApplicationCommandType.None
            && !ChatTriggersService.IsValidName(type,
                string.IsNullOrWhiteSpace(ct.ApplicationCommandName) ? ct.Trigger : ct.ApplicationCommandName))
        {
            await ReplyErrorAsync(Strings.CtInteractionNameInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var res = await Service.SetInteractionType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterName(int id, string name)
    {
        var res = await Service.SetInteractionName(ctx.Guild?.Id, id, name).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetCtInterDesc(int id, string description)
    {
        var res = await Service.SetInteractionDescription(ctx.Guild?.Id, id, description).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtInterEphemeral(int id, bool ephemeral)
    {
        var res = await Service.SetInteractionEphemeral(ctx.Guild?.Id, id, ephemeral).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the interaction errors for chat triggers.
    /// </summary>
    /// <example>.ctintererrors</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtInterErrors()
    {
        var errors = await Service.GetAcctErrors(ctx.Guild?.Id);

        var eb = new EmbedBuilder();
        var cb = new ComponentBuilder().WithButton("Support Server",
            style: ButtonStyle.Link,
            url: "https://discord.gg/Mewdeko",
            emote: Emote.Parse("<:IconInvite:778931752835088426>"));

        if (errors?.Any() ?? false)
        {
            eb.WithFields(errors.Select(x =>
                {
                    var title = x.ErrorKey switch
                    {
                        "duplicate" => Strings.CtInterrDuplicate(ctx.Guild.Id),
                        "invalid_name" => Strings.CtInterrInvalidName(ctx.Guild.Id),
                        "subcommand_match_parent" => Strings.CtInterrSubcommandMatchParent(ctx.Guild.Id),
                        "too_many_children" => Strings.CtInterrTooManyChildren(ctx.Guild.Id),
                        _ => x.ErrorKey
                    };

                    var body = x.ErrorKey switch
                    {
                        "duplicate" => Strings.CtInterrDuplicateBody(ctx.Guild.Id,
                            x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                        "invalid_name" => Strings.CtInterrInvalidNameBody(ctx.Guild.Id,
                            x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                        "subcommand_match_parent" => Strings.CtInterrSubcommandMatchParentBody(ctx.Guild.Id,
                            x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                        "too_many_children" => Strings.CtInterrTooManyChildrenBody(ctx.Guild.Id,
                            x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                        _ => x.ErrorKey
                    };

                    return new EmbedFieldBuilder()
                        .WithName(title)
                        .WithValue(body);
                }))
                .WithTitle(Strings.CtInteractionErrorsInfoTitle(ctx.Guild.Id, errors.Count))
                .WithDescription(Strings.CtInteractionErrorsInfoDesc(ctx.Guild.Id))
                .WithErrorColor();
        }
        else
        {
            eb.WithOkColor()
                .WithTitle(Strings.CtInteractionErrorsNone(ctx.Guild.Id))
                .WithDescription(Strings.CtInteractionErrorsNoneDesc(ctx.Guild.Id));
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
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
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
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Makes a chat trigger fire on a bot event rather than on a message.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="eventType">The event to listen for, or None to stop listening. <see cref="CtEventType" /></param>
    /// <remarks>
    ///     Setting an event also enables the Event trigger type, so the trigger starts responding without a second
    ///     command. Setting it back to None disables that type again.
    /// </remarks>
    /// <example>.ctevent 9987 XpLevelUp</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtEvent(int id, CtEventType eventType)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct =>
        {
            ct.EventType = (int)eventType;
            ct.ValidTriggerTypes = eventType == CtEventType.None
                ? ct.ValidTriggerTypes & ~(int)ChatTriggerType.Event
                : ct.ValidTriggerTypes | (int)ChatTriggerType.Event;
        }).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the channel an event chat trigger responds in.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="channel">The channel to respond in, or omit it to respond where the event happened.</param>
    /// <example>.cteventchannel 9987 #level-ups</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtEventChannel(int id, ITextChannel? channel = null)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.EventChannelId = channel?.Id ?? 0)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether a chat trigger replies to the message that fired it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <example>.ctreply 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtReply(int id)
    {
        return ApplyAndShow(id, ct => ct.ReplyToTrigger = !ct.ReplyToTrigger);
    }

    /// <summary>
    ///     Sets how long a chat trigger's own response stays before it is deleted.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="seconds">How many seconds to wait, or 0 to keep the response.</param>
    /// <example>.ctdeleteafter 9987 30</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtDeleteAfter(int id, int seconds)
    {
        return ApplyEconomyEdit(id, seconds, (ct, value) => ct.DeleteResponseAfter = (int)value);
    }

    /// <summary>
    ///     Sets a chat trigger's own cooldown, separate from the server-wide command cooldown.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="seconds">The cooldown in seconds, or 0 to remove it.</param>
    /// <param name="scope">Who the cooldown applies to. <see cref="CtCooldownScope" /></param>
    /// <example>.ctcooldown 9987 30 Channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtCooldown(int id, int seconds, CtCooldownScope scope = CtCooldownScope.User)
    {
        return ApplyEconomyEdit(id, seconds, (ct, value) =>
        {
            ct.CooldownSeconds = (int)value;
            ct.CooldownScope = (int)scope;
        });
    }

    /// <summary>
    ///     Requires a counter to be within a range before a chat trigger will fire.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="name">The counter's name, or "clear" to remove the requirement.</param>
    /// <param name="min">The lowest value that allows the trigger to fire, or null for no lower bound.</param>
    /// <param name="max">The highest value that allows the trigger to fire, or null for no upper bound.</param>
    /// <example>.ctrequirecounter 9987 signups 10 50</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtRequireCounter(int id, string name, long? min = null, long? max = null)
    {
        var clearing = string.Equals(name, "clear", StringComparison.OrdinalIgnoreCase);

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct =>
        {
            ct.CounterName = clearing ? null : name.ToLowerInvariant();
            ct.CounterMin = clearing ? null : min;
            ct.CounterMax = clearing ? null : max;
        }).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Puts a chat trigger into a category so it can be managed alongside related triggers.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="category">The category name, or omit it to remove the trigger from its category.</param>
    /// <example>.ctcategory 9987 welcome</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCategory(int id, [Remainder] string? category = null)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id,
            ct => ct.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim()).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            await ReplyConfirmAsync(Strings.CtCategoryCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables every chat trigger in a category at once.
    /// </summary>
    /// <param name="category">The category to act on.</param>
    /// <param name="enabled">Whether the triggers should be enabled.</param>
    /// <example>.ctcategorytoggle welcome false</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task CtCategoryToggle(string category, bool enabled)
    {
        var changed = await Service.SetCategoryDisabledAsync(ctx.Guild.Id, category, !enabled).ConfigureAwait(false);

        if (changed == 0)
        {
            await ReplyErrorAsync(Strings.CtCategoryNone(ctx.Guild.Id, category)).ConfigureAwait(false);
            return;
        }

        var state = enabled ? Strings.CtEnabledWord(ctx.Guild.Id) : Strings.CtDisabledWord(ctx.Guild.Id);
        await ReplyConfirmAsync(Strings.CtCategoryToggled(ctx.Guild.Id, changed, category, state))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Applies a change to a chat trigger and shows the result.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="apply">The change to make.</param>
    private async Task ApplyAndShow(int id, Action<ChatTrigger> apply)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, apply).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Reports whether a chat trigger would fire for a sample message, and what is blocking it if not.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="sample">The message text to test against.</param>
    /// <remarks>
    ///     Nothing is charged, sent or recorded. The test runs as the caller, so permission entries, cooldowns and
    ///     level or balance requirements are evaluated against them.
    /// </remarks>
    /// <example>.cttest 9987 hello there</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task CtTest(int id, [Remainder] string? sample = null)
    {
        var ct = await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, id).ConfigureAwait(false);

        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(sample))
        {
            await ReplyErrorAsync(Strings.CtTestNoSample(ctx.Guild.Id, id)).ConfigureAwait(false);
            return;
        }

        var (matched, blocker) = await Service
            .TestTriggerAsync(ct, (SocketGuild)ctx.Guild, (IGuildUser)ctx.User, ctx.Channel, sample)
            .ConfigureAwait(false);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.CtTestTitle(ctx.Guild.Id, id))
            .AddField(Strings.Trigger(ctx.Guild.Id), ct.Trigger?.TrimTo(1024) ?? "-")
            .AddField(Strings.CtTestMatched(ctx.Guild.Id),
                matched ? Strings.CtTestMatched(ctx.Guild.Id) : Strings.CtTestNotMatched(ctx.Guild.Id));

        if (blocker is null)
        {
            eb.WithOkColor().WithDescription(Strings.CtTestWouldFire(ctx.Guild.Id));

            var preview = ct.GetResponses().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(preview))
                eb.AddField(Strings.CtTestResponse(ctx.Guild.Id), preview.TrimTo(1024));
        }
        else
        {
            eb.WithErrorColor().AddField(Strings.CtTestBlockedBy(ctx.Guild.Id), blocker);
        }

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows how often a chat trigger has fired, and who fired it most recently.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <example>.ctstats 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task CtStats(int id)
    {
        var ct = await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, id).ConfigureAwait(false);

        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (total, recent) = await Service.GetTriggerHistoryAsync(ctx.Guild.Id, id).ConfigureAwait(false);

        if (total == 0)
        {
            await ReplyConfirmAsync(Strings.CtStatsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CtStatsTitle(ctx.Guild.Id, id))
            .AddField(Strings.CtStatsTotal(ctx.Guild.Id), total.ToString("N0"));

        var lines = recent.Select(x =>
        {
            var when = x.DateAdded.HasValue
                ? TimestampTag.FromDateTime(x.DateAdded.Value, TimestampTagStyles.Relative).ToString()
                : "-";
            return $"<@{x.UserId}> in <#{x.ChannelId}> {when}";
        }).ToList();

        if (lines.Count > 0)
            eb.AddField(Strings.CtStatsRecent(ctx.Guild.Id), string.Join("\n", lines).TrimTo(1024));

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Chains a chat trigger to another, so firing the first also runs the second.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="nextId">The ID of the trigger to chain to, or 0 to remove the chain.</param>
    /// <remarks>
    ///     The chained trigger is evaluated on its own terms, so its conditions, costs and permissions still apply.
    ///     Chains are capped in depth and cannot revisit a trigger, so a loop stops on its first repeat.
    /// </remarks>
    /// <example>.ctchain 9987 9988</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtChain(int id, int nextId)
    {
        if (id == nextId)
        {
            await ReplyErrorAsync(Strings.CtChainSelf(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (nextId != 0 && await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, nextId).ConfigureAwait(false) is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.NextTriggerId = nextId == 0 ? null : nextId)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (nextId == 0)
        {
            await ReplyConfirmAsync(Strings.CtChainCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether a chat trigger responds to messages from other bots and webhooks.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <remarks>
    ///     A trigger set this way responds only to bot messages, never to human ones. The bot never responds to its
    ///     own messages, so two triggers cannot answer each other indefinitely.
    /// </remarks>
    /// <example>.ctallowbots 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtAllowBots(int id)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AllowBots = !ct.AllowBots)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the counters chat triggers in this server read and update.
    /// </summary>
    /// <example>.ctcounters</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CtCounters()
    {
        var counters = await counterService.ListAsync(ctx.Guild.Id).ConfigureAwait(false);

        if (counters.Count == 0)
        {
            await ReplyConfirmAsync(Strings.CtCountersNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CtCountersTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", counters.Select(x =>
                Strings.CtCounterEntry(ctx.Guild.Id, x.Name, x.Value.ToString("N0")))));

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a counter to an exact value.
    /// </summary>
    /// <param name="name">The counter's name.</param>
    /// <param name="value">The value to set it to.</param>
    /// <example>.ctcounterset daysSinceIncident 0</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCounterSet(string name, long value)
    {
        name = name.ToLowerInvariant();
        await counterService.SetAsync(ctx.Guild.Id, name, value).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.CtCounterSet(ctx.Guild.Id, name, value.ToString("N0")))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a counter along with every per-user value stored under its name.
    /// </summary>
    /// <param name="name">The counter's name.</param>
    /// <example>.ctcounterdelete daysSinceIncident</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtCounterDelete(string name)
    {
        name = name.ToLowerInvariant();
        var removed = await counterService.DeleteAsync(ctx.Guild.Id, name).ConfigureAwait(false);

        await (removed == 0
                ? ReplyErrorAsync(Strings.CtCounterNotFound(ctx.Guild.Id, name))
                : ReplyConfirmAsync(Strings.CtCounterDeleted(ctx.Guild.Id, name, removed)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the placeholders other modules contribute to chat trigger responses.
    /// </summary>
    /// <example>.ctplaceholders</example>
    [Cmd]
    [Aliases]
    public async Task CtPlaceholders()
    {
        var available = Service.GetContextualPlaceholders();

        if (available.Count == 0)
        {
            await ReplyErrorAsync(Strings.CtPlaceholdersNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CtPlaceholdersTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", available.Select(x => Strings.CtCodeValue(ctx.Guild.Id, x))));

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Restricts a chat trigger to a window of the day, optionally on specific days of the week.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="start">The start of the window in 24-hour HH:mm format, or "clear" to remove the restriction.</param>
    /// <param name="end">The end of the window in 24-hour HH:mm format.</param>
    /// <param name="days">
    ///     Optional days of the week the window applies on, as names or numbers where 0 is Sunday. Defaults to every
    ///     day.
    /// </param>
    /// <example>.cttime 9987 22:00 02:00 friday saturday</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtTime(int id, string start, string? end = null, params string[] days)
    {
        if (string.Equals(start, "clear", StringComparison.OrdinalIgnoreCase))
        {
            var cleared = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.TimeConditions = null)
                .ConfigureAwait(false);

            await (cleared is null
                    ? ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id))
                    : ReplyConfirmAsync(Strings.CtTimeCleared(ctx.Guild.Id, id)))
                .ConfigureAwait(false);
            return;
        }

        if (end is null || !TimeSpan.TryParse(start, out _) || !TimeSpan.TryParse(end, out _))
        {
            await ReplyErrorAsync(Strings.CtTimeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var parsedDays = new List<int>();
        foreach (var day in days)
        {
            if (int.TryParse(day, out var dayNumber) && dayNumber is >= 0 and <= 6)
                parsedDays.Add(dayNumber);
            else if (Enum.TryParse<DayOfWeek>(day, true, out var parsedDay))
                parsedDays.Add((int)parsedDay);
        }

        var condition = new TimeCondition
        {
            StartTime = start, EndTime = end, DaysOfWeek = parsedDays.Count > 0 ? parsedDays.ToArray() : null
        };

        var json = JsonSerializer.Serialize(new[]
        {
            condition
        });

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.TimeConditions = json).ConfigureAwait(false);

        await (res is null
                ? ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id))
                : ReplyConfirmAsync(Strings.CtTimeSet(ctx.Guild.Id, id, start, end)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how long a chat trigger stays active before it stops firing.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="duration">How long the trigger stays active, or omit it to remove the expiry.</param>
    /// <example>.ctexpiry 9987 7d</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtExpiry(int id, StoopidTime? duration = null)
    {
        var expiry = duration is null ? (DateTime?)null : DateTime.UtcNow + duration.Time;
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.ExpiresAt = expiry).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (expiry is null)
        {
            await ReplyConfirmAsync(Strings.CtExpiryCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how many times a chat trigger may fire before it stops.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="uses">The maximum number of uses, or 0 to remove the limit.</param>
    /// <example>.ctmaxuses 9987 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtMaxUses(int id, int uses)
    {
        if (uses < 0)
        {
            await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.MaxUses = uses == 0 ? null : uses)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (uses == 0)
        {
            await ReplyConfirmAsync(Strings.CtMaxUsesCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how old an account must be before a chat trigger will fire for it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="age">The minimum account age, or omit it to remove the requirement.</param>
    /// <example>.ctminage 9987 7d</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtMinAge(int id, StoopidTime? age = null)
    {
        return ApplyEconomyEdit(id, (long)(age?.Time.TotalMinutes ?? 0),
            (ct, value) => ct.MinAccountAgeMinutes = (int)value);
    }

    /// <summary>
    ///     Sets how long a user must have been in the server before a chat trigger will fire for them.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="membership">The minimum membership duration, or omit it to remove the requirement.</param>
    /// <example>.ctminmember 9987 1d</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtMinMember(int id, StoopidTime? membership = null)
    {
        return ApplyEconomyEdit(id, (long)(membership?.Time.TotalMinutes ?? 0),
            (ct, value) => ct.MinServerMembershipMinutes = (int)value);
    }

    /// <summary>
    ///     Enables or disables a chat trigger without deleting it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <example>.cttoggle 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtToggle(int id)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.IsDisabled = !ct.IsDisabled)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(res.IsDisabled
                ? Strings.CtDisabled(ctx.Guild.Id, id)
                : Strings.CtEnabled(ctx.Guild.Id, id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds an extra response to a chat trigger, for use with the trigger's response mode.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="response">The response to add.</param>
    /// <example>.ctaddresponse 9987 Another possible reply</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtAddResponse(int id, [Remainder] string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AdditionalResponses =
                string.IsNullOrWhiteSpace(ct.AdditionalResponses)
                    ? response
                    : $"{ct.AdditionalResponses}@@@{response}")
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.CtResponseAdded(ctx.Guild.Id, id, res.GetResponses().Count))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes every extra response from a chat trigger, leaving its primary response in place.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <example>.ctclearresponses 9987</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtClearResponses(int id)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AdditionalResponses = null)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.CtResponsesCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how a chat trigger picks between its responses when it has more than one.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="mode">The response mode to use. <see cref="CtResponseMode" /></param>
    /// <example>.ctresponsemode 9987 Random</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtResponseMode(int id, CtResponseMode mode)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.ResponseMode = (int)mode)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how much currency a chat trigger costs the user that fires it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="amount">The amount to charge, or 0 to make the trigger free.</param>
    /// <example>.ctcost 9987 250</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtCost(int id, long amount)
    {
        return ApplyEconomyEdit(id, amount, (ct, value) => ct.CurrencyCost = value);
    }

    /// <summary>
    ///     Sets how much currency a chat trigger pays out to the user that fires it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="amount">The amount to pay out, or 0 for none.</param>
    /// <example>.ctreward 9987 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtReward(int id, long amount)
    {
        return ApplyEconomyEdit(id, amount, (ct, value) => ct.CurrencyReward = value);
    }

    /// <summary>
    ///     Sets how much XP a chat trigger grants the user that fires it.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="amount">The amount of XP to grant, or 0 for none.</param>
    /// <example>.ctxpreward 9987 25</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtXpReward(int id, int amount)
    {
        return ApplyEconomyEdit(id, amount, (ct, value) => ct.XpReward = (int)value);
    }

    /// <summary>
    ///     Sets the XP level a user must have reached before a chat trigger will fire for them.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="level">The required level, or 0 for no requirement.</param>
    /// <example>.ctreqlevel 9987 10</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public Task CtReqLevel(int id, int level)
    {
        return ApplyEconomyEdit(id, level, (ct, value) => ct.RequiredXpLevel = (int)value);
    }

    /// <summary>
    ///     Sets the message shown when a user does not meet a chat trigger's requirements.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="message">The message to show, or omit it to fail silently.</param>
    /// <example>.ctreqmsg 9987 You need 250 coins to use this.</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CtReqMsg(int id, [Remainder] string? message = null)
    {
        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.RequirementFailMessage = message)
            .ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyConfirmAsync(Strings.CtRequirementFailCleared(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Applies an economy-related edit to a chat trigger, rejecting negative amounts.
    /// </summary>
    /// <param name="id">The ID of the chat trigger.</param>
    /// <param name="amount">The amount the caller supplied.</param>
    /// <param name="apply">The assignment to perform on the trigger.</param>
    private async Task ApplyEconomyEdit(int id, long amount, Action<ChatTrigger, long> apply)
    {
        if (amount < 0)
        {
            await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => apply(ct, amount)).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.EmbedAsync(Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)))
            .ConfigureAwait(false);
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
            .WithTitle(Strings.CtInteractionErrorsTitle(ctx.Guild.Id))
            .WithDescription(Strings.CtInteractionErrorsDesc(ctx.Guild.Id))
            .WithErrorColor();
        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }
}