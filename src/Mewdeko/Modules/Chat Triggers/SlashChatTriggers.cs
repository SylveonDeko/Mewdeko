using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Attributes.SlashCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Chat_Triggers.Services;
using System.Net.Http;
using ContextType = Discord.Interactions.ContextType;

namespace Mewdeko.Modules.Chat_Triggers;

[Group("triggers", "Manage chat triggers.")]
public class SlashChatTriggers : MewdekoSlashModuleBase<ChatTriggersService>
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly InteractiveService _interactivity;

    public SlashChatTriggers(IHttpClientFactory clientFactory, InteractiveService serv)
    {
        _interactivity = serv;
        _clientFactory = clientFactory;
    }

    [SlashCommand("export", "Exports Chat Triggers into a .yml file."),
    RequireContext(ContextType.Guild), InteractionChatTriggerPermCheck(GuildPermission.Administrator),
    CheckPermissions]
    public async Task CtsExport()
    {
        await DeferAsync().ConfigureAwait(false);

        var serialized = Service.ExportCrs(ctx.Guild?.Id);
        await using var stream = await serialized.ToStream().ConfigureAwait(false);
        await FollowupWithFileAsync(stream, "cts-export.yml").ConfigureAwait(false);

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("import", "Imports Chat Triggers from a .yml file."),
    RequireContext(ContextType.Guild), InteractionChatTriggerPermCheck(GuildPermission.Administrator),
    CheckPermissions]
    public async Task CtsImport(
        [Summary("file", "The yml file to import.")] IAttachment file)
    {
        await DeferAsync().ConfigureAwait(false);

        using var client = _clientFactory.CreateClient();
        var content = await client.GetStringAsync(file.Url).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            await FollowupAsync(GetText("expr_import_no_input")).ConfigureAwait(false);
            await FollowupWithTriggerStatus();
            return;
        }

        var succ = await Service.ImportCrsAsync(ctx.User as IGuildUser, content).ConfigureAwait(false);
        if (!succ)
        {
            await FollowupAsync(GetText("expr_import_invalid_data")).ConfigureAwait(false);
            await FollowupWithTriggerStatus();
            return;
        }

        await FollowupAsync(GetText("expr_import_success")).ConfigureAwait(false);
        await FollowupWithTriggerStatus();
    }

    // respond with a modal to support multiline responces.
    [SlashCommand("add", "Add new chat trigger."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task AddChatTrigger([Summary("regex", "Should the trigger use regex.")] bool regex = false)
        => await RespondWithModalAsync<ChatTriggerModal>($"chat_trigger_add:{regex}").ConfigureAwait(false);

    [ModalInteraction("chat_trigger_add:*", true),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task AddChatTriggerModal(string sRgx, ChatTriggerModal modal)
    {
        var rgx = bool.Parse(sRgx);
        if (string.IsNullOrWhiteSpace(modal.Trigger) || string.IsNullOrWhiteSpace(modal.Message))
        {
            await RespondAsync("trigger_add_invalid").ConfigureAwait(false);
            await FollowupWithTriggerStatus();
            return;
        }

        var ct = await Service.AddAsync(ctx.Guild?.Id, modal.Trigger, modal.Message, rgx).ConfigureAwait(false);

        await RespondAsync(embed: Service.GetEmbed(ct, ctx.Guild?.Id).Build()).ConfigureAwait(false);
        await FollowupWithTriggerStatus();
    }

    [SlashCommand("edit", "Edit a chat trigger."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task EditChatTrigger
    (
        [Summary("id", "The chat trigger's id"), Autocomplete(typeof(ChatTriggerAutocompleter))] int id,
        [Summary("regex", "Should the trigger use regex.")] bool regex = false
    )
    {
        var trigger = Service.GetChatTriggers(ctx.Guild.Id, id);
        await ctx.Interaction.RespondWithModalAsync<ChatTriggerModal>($"chat_trigger_edit:{id},{regex}", null,
            x => x
                 .WithTitle("Chat Trigger Edit")
                 .UpdateTextInput("key", x => x.Value = trigger.Trigger)
                 .UpdateTextInput("message", x => x.Value = trigger.Response)).ConfigureAwait(false);

        await FollowupWithTriggerStatus();
    }

    [ModalInteraction("chat_trigger_edit:*,*", true),
     InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task EditChatTriggerModal(string sId, string sRgx, ChatTriggerModal modal)
    {
        var id = int.Parse(sId);
        var rgx = bool.Parse(sRgx);
        if (string.IsNullOrWhiteSpace(modal.Message) || id < 0)
            return;

        var cr = await Service.EditAsync(ctx.Guild?.Id, id, modal.Message, rgx, modal.Trigger).ConfigureAwait(false);
        if (cr != null)
        {
            await RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("edited_chat_trig"))
                        .WithDescription($"#{id}")
                        .AddField(efb => efb.WithName(GetText("trigger")).WithValue(cr.Trigger))
                        .AddField(efb =>
                            efb.WithName(GetText("response"))
                                .WithValue(modal.Message.Length > 1024 ? GetText("redacted_too_long") : modal.Message))
                        .Build()).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(GetText("edit_fail")).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("list", "List chat triggers."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
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

        await _interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("list-group", "List chat triggers.."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task ListChatTriggersGroup()
    {
        var chatTriggers = Service.GetChatTriggersFor(ctx.Guild?.Id);

        if (chatTriggers == null || chatTriggers.Length == 0)
        {
            await ctx.Interaction.SendErrorAsync("no_found").ConfigureAwait(false);
        }
        else
        {
            var ordered = chatTriggers
                .GroupBy(ct => ct.Trigger)
                .OrderBy(ct => ct.Key)
                .ToList();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(chatTriggers.Length / 20)
                .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await _interactivity.SendPaginatorAsync(paginator, Context.Interaction as SocketInteraction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(GetText("name"))
                                                        .WithDescription(string.Join("\r\n",
                                                            ordered.Skip(page * 20).Take(20).Select(ct =>
                                                                $"**{ct.Key.Trim().ToLowerInvariant()}** `x{ct.Count()}`")));
            }
        }

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("show", "Shows the responce of a chat trigger.")]
    public async Task ShowChatTrigger([Summary("id", "The chat trigger's id"), Autocomplete(typeof(ChatTriggerAutocompleter))] int id)
    {
        var found = Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (found == null)
            await ctx.Interaction.SendErrorAsync(GetText("no_found_id")).ConfigureAwait(false);
        else
            await ctx.Interaction.RespondAsync(embed: Service.GetEmbed(found, ctx.Guild?.Id).Build()).ConfigureAwait(false);

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("delete", "delete a chat trigger."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task DeleteChatTrigger([Summary("id", "The chat trigger's id"), Autocomplete(typeof(ChatTriggerAutocompleter))] int id)
    {
        var ct = await Service.DeleteAsync(ctx.Guild?.Id, id).ConfigureAwait(false);

        if (ct != null)
        {
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                            .WithTitle(GetText("deleted"))
                            .WithDescription($"#{ct.Id}")
                            .AddField(efb => efb.WithName(GetText("trigger")).WithValue(ct.Trigger.TrimTo(1024)))
                            .AddField(efb => efb.WithName(GetText("response")).WithValue(ct.Response.TrimTo(1024)))
                            .Build()).ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendErrorAsync(GetText("no_found_id")).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("react", "add a reaction chat trigger.."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task CtReact
    (
        [Summary("id", "The chat trigger's id"), Autocomplete(typeof(ChatTriggerAutocompleter))] int id,
        [Summary("emoji", "A space-seperated list of emojis to react with")] string emoji
    )
    {
        var ct = Service.GetChatTriggers(Context.Guild?.Id, id);
        if (ct is null)
        {
            await ctx.Interaction.SendErrorAsync(GetText("no_found")).ConfigureAwait(false);
            return;
        }

        var emojiStrs = emoji.Split(' ');

        if (emojiStrs.Length == 0)
        {
            await Service.ResetCrReactions(ctx.Guild?.Id, id).ConfigureAwait(false);
            await ctx.Interaction.SendErrorAsync(GetText("ctr_reset")).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(GetText("ctr_testing_emotes")).ConfigureAwait(false);
        var message = await ctx.Interaction.GetOriginalResponseAsync();
        var succ = new List<string>();
        foreach (var emojiStr in emojiStrs)
        {
            var emote = emojiStr.ToIEmote();

            // i should try adding these emojis right away to the message, to make sure the bot can react with these emojis. If it fails, skip that emoji
            try
            {
                await message.AddReactionAsync(emote).ConfigureAwait(false);
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
            await message.ModifyAsync(x => x.Embed = new EmbedBuilder().WithErrorColor()
                                        .WithDescription(GetText("invalid_emojis", Format.Bold(id.ToString())))
                                        .Build()).ConfigureAwait(false);
            return;
        }

        await Service.SetCrReactions(ctx.Guild?.Id, id, succ).ConfigureAwait(false);

        var text = GetText("ctr_set", Format.Bold(id.ToString()), string.Join(',', succ.Select(x => x.ToString())));
        await message.ModifyAsync(x => x.Embed = new EmbedBuilder().WithOkColor().WithDescription(text).Build()).ConfigureAwait(false);

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("toggle-option", "Edit chat trigger options."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task InternalCtEdit
    (
        [Summary("id", "The chat trigger's id"), Autocomplete(typeof(ChatTriggerAutocompleter))] int id,
        [Summary("option", "The option to toggle")] ChatTriggersService.CtField option
    )
    {
        var ct = Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            return;
        }
        var (success, newVal) = await Service.ToggleCrOptionAsync(ct, option).ConfigureAwait(false);

        if (!success)
        {
            await ctx.Interaction.SendConfirmAsync(GetText("no_found_id")).ConfigureAwait(false);
            return;
        }

        if (newVal)
        {
            await ctx.Interaction.SendConfirmAsync(GetText("option_enabled", Format.Code(option.ToString()),
                        Format.Code(id.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync(GetText("option_dissabled", Format.Code(option.ToString()),
                        Format.Code(id.ToString()))).ConfigureAwait(false);
        }
        await FollowupWithTriggerStatus();
    }

    [SlashCommand("valid-types", "Change the valid types of the trigger"),
     InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task ChatTriggerValidType(
        [Summary("trigger", "The chat trigger to edit."), Autocomplete(typeof(ChatTriggerAutocompleter))]int id,
        [Summary("type", "The type to enable/disable.")] ChatTriggerType type,
        [Summary("enabled", "Should the type be enabled?")] bool enabled)
    {
        var res = await Service.SetValidTriggerType(ctx.Guild?.Id, id, type, enabled).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus();
    }

    [SlashCommand("clear", "Clear all chat triggers."),
    InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
    public async Task CtsClear()
    {
        await DeferAsync().ConfigureAwait(false);
        if (await PromptUserConfirmAsync(new EmbedBuilder()
                    .WithTitle("Chat triggers clear")
                    .WithDescription("This will delete all chat triggers on this server."), ctx.User.Id, false).ConfigureAwait(false))
        {
            var count = Service.DeleteAllChatTriggers(ctx.Guild.Id);
            await ConfirmLocalizedAsync(GetText("cleared", count)).ConfigureAwait(false);
        }
        await FollowupWithTriggerStatus();
    }


    [Group("roles", "roles")]
    public class Roles : MewdekoSlashModuleBase<ChatTriggersService>
    {
        [SlashCommand("add", "Toggle whether running this command will add the role to the user."),
        InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task CtrGrantToggle
        (
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to add roles to.")] int id,
            [Summary("role", "The roll to toggle.")] IRole role
        )
        {
            var gUsr = ctx.User as IGuildUser;

            if (!role.CanManageRole(gUsr))
            {
                await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
                await FollowupWithTriggerStatus();
                return;
            }

            var ct = Service.GetChatTriggers(ctx.Guild?.Id, id);

            if (ct is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
                await FollowupWithTriggerStatus();
                return;
            }

            var toggleDisabled = ct.IsToggled(role.Id);

            await Service.ToggleGrantedRole(ct, role.Id).ConfigureAwait(false);

            var str = toggleDisabled
                ? "ct_role_toggle_disabled"
                : ct.IsToggled(role.Id)
                    ? "ct_role_toggle_enabled"
                    : ct.IsGranted(role.Id)
                        ? "ct_role_add_enabled"
                        : "ct_role_add_disabled";

            await ReplyConfirmLocalizedAsync(str, Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);

            await FollowupWithTriggerStatus();
        }

        [SlashCommand("toggle-remove", "Toggle whether running this command will remove the role to the user."),
        InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task CtrRemoveToggle
        (
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to remove roles from.")] int id,
            [Summary("role", "The roll to toggle.")] IRole role
        )
        {
            var gUsr = ctx.User as IGuildUser;

            if (!role.CanManageRole(gUsr))
            {
                await ReplyErrorLocalizedAsync("cant_manage_role").ConfigureAwait(false);
                return;
            }

            var ct = Service.GetChatTriggers(ctx.Guild?.Id, id);
            if (ct is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
                return;
            }

            var toggleDisabled = ct.IsToggled(role.Id);

            await Service.ToggleRemovedRole(ct, role.Id).ConfigureAwait(false);

            var str = toggleDisabled
                ? "ct_role_toggle_disabled"
                : ct.IsToggled(role.Id)
                    ? "ct_role_toggle_enabled"
                    : ct.IsRemoved(role.Id)
                        ? "ct_role_remove_enabled"
                        : "cr_role_remove_disabled";

            await ReplyConfirmLocalizedAsync(str, Format.Bold(role.Name), Format.Code(id.ToString())).ConfigureAwait(false);

            await FollowupWithTriggerStatus();
        }

        [SlashCommand("mode", "Changes the way roles are added to chat triggers."), CheckPermissions, InteractionChatTriggerPermCheck(GuildPermission.Administrator)]
        public async Task ChatTriggerRoleGrantType(
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to remove roles from.")] int id,
            [Summary("mode", "How should roles be added when the trigger is used.")] CTRoleGrantType type)
        {
            var res = await Service.SetRoleGrantType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build());
            }

            await FollowupWithTriggerStatus();
        }

        private async Task FollowupWithTriggerStatus()
        {
            var errors = Service.GetACCTErrors(ctx.Guild?.Id);
            if (!(errors?.Any() ?? false)) return;
            var embed = new EmbedBuilder()
                        .WithTitle(GetText("ct_interaction_errors_title"))
                        .WithDescription(GetText("ct_interaction_errors_desc"))
                        .WithErrorColor();
            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    [Group("interactions", "interactions")]
    public class Interactions : MewdekoSlashModuleBase<ChatTriggersService>
    {
        [SlashCommand("type", "Sets the type of interaction support (user, message, or slash)."),
         InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task SetCommandType(
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to update.")] int id,
            [Summary("type", "The type of command, use 'none' to disable commands in their entirety.")]
            CTApplicationCommandType type)
        {
            var ct = Service.GetChatTriggers(ctx.Guild?.Id, id);
            if (ct is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
                return;
            }
            
            // validate the name based on type
            if (type != CTApplicationCommandType.None && !ChatTriggersService.IsValidName(type,
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
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus();
        }

        [SlashCommand("name", "Sets the name of the interaction."),
         InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task SetName(
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to update.")] int id,
            [Summary("name", "The name of the interaction.")] string name)
        {
            var res = await Service.SetInteractionName(ctx.Guild?.Id, id, name).ConfigureAwait(false);
            
            if (res is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus();
        }
        
        [SlashCommand("description", "Sets the description of the interaction."),
         InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task SetDescription(
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to update.")] int id,
            [Summary("description", "The description of the interaction.")] string description)
        {
            var res = await Service.SetInteractionDescription(ctx.Guild?.Id, id, description).ConfigureAwait(false);
            
            if (res is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus();
        }
        
        [SlashCommand("ephemeral", "Enables/Disables ephemeral mode."),
         InteractionChatTriggerPermCheck(GuildPermission.Administrator), CheckPermissions]
        public async Task ToggleEphemeral(
            [Autocomplete(typeof(ChatTriggerAutocompleter)), Summary("trigger", "The trigger to update.")] int id,
            [Summary("ephemeral", "Should the trigger be ephemeral?")] bool ephemeral)
        {
            var res = await Service.SetInteractionEphemeral(ctx.Guild?.Id, id, ephemeral).ConfigureAwait(false);
            
            if (res is null)
            {
                await ReplyErrorLocalizedAsync("no_found_id").ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, GetText("edited_chat_trig")).Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus();
        }

        private async Task FollowupWithTriggerStatus()
        {
            var errors = Service.GetACCTErrors(ctx.Guild?.Id);
            if (!(errors?.Any() ?? false)) return;
            var embed = new EmbedBuilder()
                        .WithTitle(GetText("ct_interaction_errors_title"))
                        .WithDescription(GetText("ct_interaction_errors_desc"))
                        .WithErrorColor();
            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true);
        }

        [SlashCommand("errors", "Check for errors in your interaction chat triggers."), CheckPermissions, InteractionChatTriggerPermCheck(GuildPermission.Administrator)]
        private async Task Errors()
        {
            var errors = Service.GetACCTErrors(ctx.Guild?.Id);
            var eb = new EmbedBuilder();
            var cb = new ComponentBuilder().WithButton("Support Server", style:ButtonStyle.Link, url:"https://discord.gg/Mewdeko", emote:Emote.Parse("<:IconInvite:778931752835088426>"));
            if (errors?.Any() ?? false)
            {
                eb.WithFields(errors.Select(x =>
                      new EmbedFieldBuilder().WithName(GetText($"ct_interr_{x.ErrorKey}")).WithValue(
                          GetText($"ct_interr_{x.ErrorKey}_body", x.CtRealNames.Select(x => $" - {x}").Join('\n')))))
                  .WithTitle(GetText("ct_interaction_errors_info_title", errors.Count))
                  .WithDescription(GetText("ct_interaction_errors_info_desc")).WithErrorColor();
            }
            else
            {
                eb.WithOkColor().WithTitle(GetText("ct_interaction_errors_none"))
                  .WithDescription(GetText("ct_interaction_errors_none_desc"));
            }

            await RespondAsync(embed: eb.Build(), components: cb.Build());
        }
    }

    private async Task FollowupWithTriggerStatus()
    {
        var errors = Service.GetACCTErrors(ctx.Guild?.Id);
        if (!(errors?.Any() ?? false)) return;
        var embed = new EmbedBuilder()
            .WithTitle(GetText("ct_interaction_errors_title"))
            .WithDescription(GetText("ct_interaction_errors_desc"))
            .WithErrorColor();
        await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
}
