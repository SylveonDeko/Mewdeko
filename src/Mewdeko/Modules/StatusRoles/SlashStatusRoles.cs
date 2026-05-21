using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.StatusRoles.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.StatusRoles;

/// <summary>
///     Slash Module for managing roles that are assigned when a user has a specific status.
/// </summary>
[Group("statusroles", "Manage roles that are assigned when a user has a specific status.")]
public class SlashStatusRoles(BotConfigService bss, InteractiveService interactivity)
    : MewdekoSlashModuleBase<StatusRolesService>
{
    /// <summary>
    ///     Adds a status to watch for.
    /// </summary>
    /// <param name="status">The status to add.</param>
    [SlashCommand("add-status-role", "Adds a status to watch for")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task AddStatusRole(string status)
    {
        if (status.Length > 128)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.StatusTooLong(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        var added = await Service.AddStatusRoleConfig(status, ctx.Guild.Id);
        if (added)
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusRoleConfigAdded(ctx.Guild.Id));
        else
            await ctx.Interaction.SendErrorAsync(Strings.StatusRoleExists(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
    }

    /// <summary>
    ///     Removes an existing statusrole.
    /// </summary>
    /// <param name="potentialStatusRole">The status role to remove.</param>
    [SlashCommand("remove-status-role", "Removes an existing statusrole")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveStatusRole(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole)
    {
        await Service.RemoveStatusRoleConfig(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync(Strings.StatusroleRemoved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets or previews an embed for a specific status role.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to set or preview the embed for.</param>
    /// <param name="embedText">The embed text to set.</param>
    [SlashCommand("set-embed", "Sets or previews an embed for a specific status role")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleEmbed(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole, string embedText = null)
    {
        if (string.IsNullOrWhiteSpace(embedText))
        {
            if (string.IsNullOrWhiteSpace(potentialStatusRole.StatusEmbed))
            {
                await ctx.Interaction.SendErrorAsync(
                    Strings.StatusroleNoEmbed(ctx.Guild.Id, bss.Data.ErrorEmote),
                    Config);
                return;
            }

            await DeferAsync();

            var componentBuilder = new ComponentBuilder()
                .WithButton("Preview", "preview")
                .WithButton("View Raw", "viewraw");

            var msgid = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(
                    Strings.StatusroleTextSelection(ctx.Guild.Id, bss.Data.LoadingEmote))
                .Build(), components: componentBuilder.Build());

            var button = await GetButtonInputAsync(ctx.Interaction.Id, msgid.Id, ctx.User.Id);
            switch (button)
            {
                case "preview":
                    var rep = new ReplacementBuilder()
                        .WithDefault(ctx).Build();
                    if (SmartEmbed.TryParse(rep.Replace(potentialStatusRole.StatusEmbed), ctx.Guild.Id, out var embeds,
                            out var plainText, out var components))
                        await ctx.Interaction.FollowupAsync(plainText, embeds, components: components.Build());
                    else
                        await ctx.Interaction.FollowupAsync(rep.Replace(potentialStatusRole.StatusEmbed));
                    break;
                case "viewraw":
                    await ctx.Interaction.SendConfirmFollowupAsync(potentialStatusRole.StatusEmbed);
                    break;
                default:
                    await ctx.Interaction.SendErrorFollowupAsync($"{bss.Data.ErrorEmote} Timed out.", Config);
                    break;
            }
        }
        else
        {
            await Service.SetStatusEmbed(potentialStatusRole, embedText);
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusroleEmbedTextSuccess(ctx.Guild.Id, bss.Data.SuccessEmote));
        }
    }

    /// <summary>
    ///     Sets the channel the embed will use for this StatusRole.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to set the channel for.</param>
    /// <param name="channel">The channel to set.</param>
    [SlashCommand("set-channel", "Sets the channel the embed will use for this StatusRole")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleChannel(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole, ITextChannel channel)
    {
        if (potentialStatusRole.StatusChannelId == channel.Id)
        {
            await ctx.Interaction.SendErrorAsync(Strings.AlreadyStatusEmbedChannel(ctx.Guild.Id, bss.Data.ErrorEmote),
                Config);
            return;
        }

        await Service.SetStatusChannel(potentialStatusRole, channel.Id);
        await ctx.Interaction.SendConfirmAsync(
            Strings.StatusRoleChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets the roles to add when a user has the selected status.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to set the add roles for.</param>
    /// <param name="roles">The roles to add.</param>
    [SlashCommand("set-add-roles", "Sets the roles to add when a user has the selected status")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetAddRoles([Autocomplete(typeof(StatusRoleAutocompleter))] StatusRole potentialStatusRole,
        IRole[] roles)
    {
        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToAdd))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetAddRoles(potentialStatusRole, splitRoleIds);
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusRolesAdded(ctx.Guild.Id, bss.Data.SuccessEmote,
                    string.Join("|", roles.Select(x => x.Mention))));
        }
        else
        {
            var toModify = potentialStatusRole.ToAdd.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetAddRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusRolesAdded(ctx.Guild.Id, bss.Data.SuccessEmote,
                    string.Join("|", toModify.Select(x => $"<@&{x}>"))));
        }
    }

    /// <summary>
    ///     Sets roles to be removed when a user has a certain status.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to set the remove roles for.</param>
    /// <param name="roles">The roles to remove.</param>
    [SlashCommand("set-remove-roles", "Set roles to be removed when a user has a certain status")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetRemoveRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole, IRole[] roles)
    {
        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToRemove))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetRemoveRoles(potentialStatusRole, splitRoleIds);
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusRolesRemoved(ctx.Guild.Id, bss.Data.SuccessEmote,
                    string.Join("|", roles.Select(x => x.Mention))));
        }
        else
        {
            var toModify = potentialStatusRole.ToRemove.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", toModify));
            await ctx.Interaction.SendConfirmAsync(
                Strings.StatusRolesRemoved(ctx.Guild.Id, bss.Data.SuccessEmote,
                    string.Join("|", toModify.Select(x => $"<@&{x}>"))));
        }
    }

    /// <summary>
    ///     Removes one or more roles from the roles added when a user has a certain status.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to remove the add roles from.</param>
    /// <param name="roles">The roles to remove.</param>
    [SlashCommand("remove-add-roles", "Remove one or more roles from the roles added when a user has a certain status")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveAddRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole, IRole[] roles)
    {
        var addRoles = potentialStatusRole.ToAdd.Split(" ");
        var newList = addRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (addRoles.Length == newList.Count)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.StatusAddrolesNoneRemoved(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        await Service.SetAddRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Interaction.SendConfirmAsync(
            Strings.StatusAddroleSuccessfullyRemoved(ctx.Guild.Id, bss.Data.SuccessEmote,
                string.Join("|", roles.Select(x => x.Mention))));
    }

    /// <summary>
    ///     Removes one or more roles from the roles removed when a user has a certain status.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to remove the remove roles from.</param>
    /// <param name="roles">The roles to remove.</param>
    [SlashCommand("remove-remove-roles",
        "Remove one or more roles from the roles removed when a user has a certain status")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveRemoveRoles(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole, params IRole[] roles)
    {
        var removeRoles = potentialStatusRole.ToRemove.Split(" ");
        var newList = removeRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (removeRoles.Length == newList.Count)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.StatusRemoveroleNoneRemoved(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        await Service.SetRemoveRoles(potentialStatusRole, string.Join(" ", newList));
        await ctx.Interaction.SendConfirmAsync(
            Strings.StatusRemoveroleSuccessfullyRemoved(ctx.Guild.Id, bss.Data.SuccessEmote,
                string.Join("|", roles.Select(x => x.Mention))));
    }

    /// <summary>
    ///     Toggles whether added roles are removed when a status is removed.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to toggle.</param>
    [SlashCommand("toggle-remove-added", "Toggles whether added roles are removed when a status is removed")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleRemoveAdded(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole)
    {
        var returned = await Service.ToggleRemoveAdded(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync(Strings.StatusRemoveAddedNow(ctx.Guild.Id, bss.Data.SuccessEmote,
            returned));
    }

    /// <summary>
    ///     Toggles whether removed roles are readded when a status is removed.
    /// </summary>
    /// <param name="potentialStatusRole">The potential status role to toggle.</param>
    [SlashCommand("toggle-readd-removed", "Toggles whether removed roles are readded when a status is removed")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleReaddRemoved(
        [Autocomplete(typeof(StatusRoleAutocompleter))]
        StatusRole potentialStatusRole)
    {
        var returned = await Service.ToggleAddRemoved(potentialStatusRole);
        await ctx.Interaction.SendConfirmAsync(Strings.StatusReaddRemovedNow(ctx.Guild.Id, bss.Data.SuccessEmote,
            returned));
    }

    /// <summary>
    ///     Lists all current status roles with their index.
    /// </summary>
    [SlashCommand("list", "Lists all current status roles with their index")]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ListStatusRoles()
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Interaction.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.NoConfiguredStatusroles(ctx.Guild.Id)}", Config);
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