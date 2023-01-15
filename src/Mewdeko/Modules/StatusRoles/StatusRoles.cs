using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.StatusRoles.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.StatusRoles;

public class StatusRoles : MewdekoModuleBase<StatusRolesService>
{
    private readonly BotConfigService bss;
    private readonly InteractiveService interactivity;

    public StatusRoles(BotConfigService bss, InteractiveService interactivity)
    {
        this.bss = bss;
        this.interactivity = interactivity;
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task AddStatusRole([Remainder] string status)
    {
        if (status.Length > 128)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} That's too long to even fit in a normal status. Try again.");
            return;
        }

        await Service.AddStatusRoleConfig(status, ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync("Added StatusRole config! Please configure it with the other commands.");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleEmbed(int index, [Remainder] string embedText = null)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        if (string.IsNullOrWhiteSpace(embedText))
        {
            if (string.IsNullOrWhiteSpace(potentialStatusRole.StatusEmbed))
            {
                await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There is no embed/text set for this StatusRole! Please include embed json or text to preview it!");
                return;
            }

            var componentBuilder = new ComponentBuilder()
                .WithButton("Preview", "preview")
                .WithButton("View Raw", "viewraw");

            var msgid = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription($"{bss.Data.LoadingEmote} Please select what you want to do with the current StatusRole text")
                .Build(), components: componentBuilder.Build());

            var button = await GetButtonInputAsync(ctx.Channel.Id, msgid.Id, ctx.User.Id);
            switch (button)
            {
                case "preview":
                    var rep = new ReplacementBuilder()
                        .WithDefault(ctx).Build();
                    if (SmartEmbed.TryParse(rep.Replace(potentialStatusRole.StatusEmbed), ctx.Guild.Id, out var embeds, out var plainText, out var components))
                        await ctx.Channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
                    else
                        await ctx.Channel.SendMessageAsync(rep.Replace(potentialStatusRole.StatusEmbed));
                    break;
                case "viewraw":
                    await ctx.Channel.SendConfirmAsync(potentialStatusRole.StatusEmbed);
                    break;
                default:
                    await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} Timed out.");
                    break;
            }
        }
        else
        {
            await Service.SetStatusEmbed(potentialStatusRole.Id, embedText);
            await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully set embed text!");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetStatusRoleChannel(int index, ITextChannel channel)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        if (potentialStatusRole.StatusChannelId == channel.Id)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} That's already your StatusEmbedChannel.");
            return;
        }

        await Service.SetStatusChannel(potentialStatusRole.Id, channel.Id);
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully set StatusEmbedChannel to {channel.Mention}!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetAddRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToAdd))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetAddRoles(index, splitRoleIds);
            await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToAdd.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetAddRoles(index, string.Join(" ", toModify));
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now add the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task SetRemoveRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        if (string.IsNullOrWhiteSpace(potentialStatusRole.ToRemove))
        {
            var splitRoleIds = string.Join(" ", roles.Select(x => x.Id));
            await Service.SetRemoveRoles(index, splitRoleIds);
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", roles.Select(x => x.Mention))}");
        }
        else
        {
            var toModify = potentialStatusRole.ToRemove.Split(" ").ToList();
            toModify.AddRange(roles.Select(x => x.Id.ToString()));
            await Service.SetRemoveRoles(index, string.Join(" ", toModify));
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} Having this status will now remove the following roles:\n{string.Join("|", toModify.Select(x => $"<@&{x}>"))}");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveAddRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        var addRoles = potentialStatusRole.ToAdd.Split(" ");
        var newList = addRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (addRoles.Length == newList.Count)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No AddRoles removed, none of the provided roles are in the list.");
            return;
        }

        await Service.SetAddRoles(index, string.Join(" ", newList));
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully removed the following roles from AddRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveRemoveRoles(int index, params IRole[] roles)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        var removeRoles = potentialStatusRole.ToRemove.Split(" ");
        var newList = removeRoles.Except(roles.Select(x => $"{x.Id}")).ToList();
        if (removeRoles.Length == newList.Count)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No RemoveRoles removed, none of the provided roles are in the list.");
            return;
        }

        await Service.SetRemoveRoles(index, string.Join(" ", newList));
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} Succesfully removed the following roles from RemoveRoles\n{string.Join("|", roles.Select(x => x.Mention))}");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleRemoveAdded(int index)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        var returned = await Service.ToggleRemoveAdded(index);
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} RemoveAdded is now `{returned}`");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleReaddRemoved(int index)
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var potentialStatusRole = statusRoles.ElementAt(index - 1);
        if (potentialStatusRole is null)
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} No StatusRole found with that ID!");
            return;
        }

        var returned = await Service.ToggleAddRemoved(index);
        await ctx.Channel.SendConfirmAsync($"{bss.Data.SuccessEmote} ReaddRemoved is now `{returned}`");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild)]
    public async Task ListStatusRoles()
    {
        var statusRoles = await Service.GetStatusRoleConfig(ctx.Guild.Id);
        if (!statusRoles.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} There are no configure StatusRoles!");
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(statusRoles.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
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
                    $"\n`Message:` {curStatusRole.StatusEmbed.TrimTo(1000)}")
                .WithOkColor();
        }
    }
}