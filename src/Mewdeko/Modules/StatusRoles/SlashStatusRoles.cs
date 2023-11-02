using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.StatusRoles.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.StatusRoles;

[Group("statusroles", "Manage roles that are assigned when a user has a specific status.")]
public class SlashStatusRoles : MewdekoSlashModuleBase<StatusRolesService>
{
    private readonly BotConfigService bss;
    private readonly InteractiveService interactivity;

    public SlashStatusRoles(BotConfigService bss, InteractiveService interactivity)
    {
        this.bss = bss;
        this.interactivity = interactivity;
    }

    [SlashCommand("add-status-role", "Adds a status to watch for"), SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task AddStatusRole(string status)
    {
        if (status.Length > 128)
        {
            await ctx.Interaction.SendErrorAsync(
                $"{bss.Data.ErrorEmote} That's too long to even fit in a normal status. Try again.");
            return;
        }

        var added = await Service.AddStatusRoleConfig(status, ctx.Guild.Id);
        if (added)
            await ctx.Interaction.SendConfirmAsync(
                "Added StatusRole config! Please configure it with the other commands.");
        else
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} That StatusRole already exists!");
    }

    [SlashCommand("remove-status-role", "Removes an existing statusrole"), SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveStatusRole(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole)
    {
        await Service.RemoveStatusRoleConfig(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync("StatusRole config removed!");
    }

    [SlashCommand("set-embed", "Sets or previews an embed for a specific status role"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleEmbed(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole, string embedText = null)
    {
        if (string.IsNullOrWhiteSpace(embedText))
        {
            if (string.IsNullOrWhiteSpace(potentialStatusRole.StatusEmbed))
            {
                await ctx.Interaction.SendErrorAsync(
                    $"{bss.Data.ErrorEmote} There is no embed/text set for this StatusRole! Please include embed json or text to preview it!");
                return;
            }

            await DeferAsync();

            var componentBuilder = new ComponentBuilder()
                .WithButton("Preview", "preview")
                .WithButton("View Raw", "viewraw");

            var msgid = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(
                    $"{bss.Data.LoadingEmote} Please select what you want to do with the current StatusRole text")
                .Build(), components: componentBuilder.Build());

            var button = await GetButtonInputAsync(ctx.Interaction.Id, msgid.Id, ctx.User.Id);
            switch (button)
            {
                case "preview":
                    var rep = new ReplacementBuilder()
                        .WithDefault(ctx).Build();
                    if (SmartEmbed.TryParse(rep.Replace(potentialStatusRole.StatusEmbed), ctx.Guild.Id, out var embeds,
                            out var plainText, out var components))
                        await ctx.Interaction.FollowupAsync(plainText, embeds: embeds, components: components.Build());
                    else
                        await ctx.Interaction.FollowupAsync(rep.Replace(potentialStatusRole.StatusEmbed));
                    break;
                case "viewraw":
                    await ctx.Interaction.SendConfirmFollowupAsync(potentialStatusRole.StatusEmbed);
                    break;
                default:
                    await ctx.Interaction.SendErrorFollowupAsync($"{bss.Data.ErrorEmote} Timed out.");
                    break;
            }
        }
        else
        {
            await Service.SetStatusEmbed(potentialStatusRole, embedText);
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully set embed text!");
        }
    }

    [SlashCommand("set-channel", "Sets the channel the embed will use for this StatusRole"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleChannel(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole, ITextChannel channel)
    {
        if (potentialStatusRole.StatusChannelId == channel.Id)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} That's already your StatusEmbedChannel.");
            return;
        }

        await Service.SetStatusChannel(potentialStatusRole, channel.Id);
        await ctx.Interaction.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully set StatusEmbedChannel to {channel.Mention}!");
    }

    [SlashCommand("set-add-roles", "Sets the roles to add when a user has the selected status"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetAddRoles([Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole,
        IRole[] roles)
    {
        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToAdd))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetAddRoles(potentialStatusRole, splitRoleIds);
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToAdd.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetAddRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    [SlashCommand("set-remove-roles", "Set roles to be removed when a user has a certain status"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetRemoveRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole, IRole[] roles)
    {
        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToRemove))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetRemoveRoles(potentialStatusRole, splitRoleIds);
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToRemove.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    [SlashCommand("remove-add-roles", "Remove one or more roles from the roles added when a user has a certain status"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveAddRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole, IRole[] roles)
    {
        var addRoles = potentialStatusRole.ToAdd.Split(" ");
        var newList = addRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (addRoles.Length == newList.Count)
        {
            await ctx.Interaction.SendErrorAsync(
                $"{bss.Data.ErrorEmote} No AddRoles removed, none of the provided roles are in the list.");
            return;
        }

        await Service.SetAddRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Interaction.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully removed the following roles from AddRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    [SlashCommand("remove-remove-roles",
         "Remove one or more roles from the roles removed when a user has a certain status"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveRemoveRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole, params IRole[] roles)
    {
        var removeRoles = potentialStatusRole.ToRemove.Split(" ");
        var newList = removeRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (removeRoles.Length == newList.Count)
        {
            await ctx.Interaction.SendErrorAsync(
                $"{bss.Data.ErrorEmote} No RemoveRoles removed, none of the provided roles are in the list.");
            return;
        }

        await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Interaction.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} Succesfully removed the following roles from RemoveRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    [SlashCommand("toggle-remove-added", "Toggles whether added roles are removed when a status is removed"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleRemoveAdded(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole)
    {
        var returned = await Service.ToggleRemoveAdded(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} RemoveAdded is now `{returned}`");
    }

    [SlashCommand("toggle-readd-removed", "Toggles whether removed roles are readded when a status is removed"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleReaddRemoved(
        [Autocomplete(typeof(StatusRoleAutocompleter))] StatusRolesTable potentialStatusRole)
    {
        var returned = await Service.ToggleAddRemoved(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} ReaddRemoved is now `{returned}`");
    }

    [SlashCommand("list", "Lists all current status roles with their index"),
     SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ListStatusRoles()
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configured StatusRoles!");
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(statusRoles.Count() - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var statusArray = statusRoles.ToArray();
            var curStatusRole = statusArray.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                    $"#{Array.IndexOf(statusArray, curStatusRole) + 1}" +
                    $"\n`Status`: {curStatusRole.Status.TrimTo(30)}" +
                    $"\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curStatusRole.StatusChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curStatusRole.StatusChannelId).ConfigureAwait(false))?.Mention)} {curStatusRole.StatusChannelId}" +
                    $"\n`AddRoles`: {(!string.IsNullOrEmpty(curStatusRole.ToAdd) ? string.Join("|", curStatusRole.ToAdd.Split(" ").Select(x => $"<@&{x}>")) : "None")}" +
                    $"\n`RemoveRoles`: {(!string.IsNullOrEmpty(curStatusRole.ToRemove) ? string.Join("|", curStatusRole.ToRemove.Split(" ").Select(x => $"<@&{x}>")) : "None")}" +
                    $"\n`RemoveAdded`: {curStatusRole.RemoveAdded}" +
                    $"\n`ReaddRemoved`: {curStatusRole.ReaddRemoved}" +
                    $"\n`Message:` {(curStatusRole.StatusEmbed.IsNullOrWhiteSpace() ? "None" : curStatusRole.StatusEmbed.TrimTo(100))}")
                .WithOkColor();
        }
    }
}