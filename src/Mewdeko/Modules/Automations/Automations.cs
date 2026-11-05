using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Automations.Common;
using Mewdeko.Modules.Automations.Services;

namespace Mewdeko.Modules.Automations;

/// <summary>
///     Commands for viewing and managing automations. Building them is done in the dashboard.
/// </summary>
public class Automations : MewdekoModuleBase<AutomationService>
{
    /// <summary>
    ///     Lists the automations of the server.
    /// </summary>
    /// <example>.automations</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationList()
    {
        var automations = await Service.GetAllAsync(ctx.Guild.Id);
        if (automations.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutomationNone(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: AutomationDisplay.List(Strings, ctx.Guild.Id, automations).Build());
    }

    /// <summary>
    ///     Shows one automation.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationshow 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationShow(int id)
    {
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        if (automation is null)
        {
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
            return;
        }

        await ctx.Channel.SendMessageAsync(embed: AutomationDisplay.Show(Strings, ctx.Guild.Id, automation).Build());
    }

    /// <summary>
    ///     Turns an automation on.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationenable 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationEnable(int id)
    {
        var automation = await Service.SetEnabledAsync(ctx.Guild.Id, id, true);
        if (automation is null)
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
        else
            await ReplyConfirmAsync(Strings.AutomationEnabled(ctx.Guild.Id, automation.Name));
    }

    /// <summary>
    ///     Turns an automation off.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationdisable 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationDisable(int id)
    {
        var automation = await Service.SetEnabledAsync(ctx.Guild.Id, id, false);
        if (automation is null)
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
        else
            await ReplyConfirmAsync(Strings.AutomationDisabled(ctx.Guild.Id, automation.Name));
    }

    /// <summary>
    ///     Deletes an automation.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationdelete 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationDelete(int id)
    {
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        if (automation is null || !await Service.DeleteAsync(ctx.Guild.Id, id))
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
        else
            await ReplyConfirmAsync(Strings.AutomationDeleted(ctx.Guild.Id, automation.Name));
    }

    /// <summary>
    ///     Runs an automation once, as if you had triggered it here.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationtest 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationTest(int id)
    {
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        var result = automation is null
            ? null
            : await Service.RunTestAsync((SocketGuild)ctx.Guild, id, (IGuildUser)ctx.User, ctx.Channel,
                ctx.Message as IUserMessage);

        if (automation is null || result is null)
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
        else if (result.Success)
            await ReplyConfirmAsync(Strings.AutomationTestOk(ctx.Guild.Id, automation.Name));
        else
            await ReplyErrorAsync(Strings.AutomationTestFailed(ctx.Guild.Id, automation.Name,
                (result.FailedIndex ?? 0) + 1, result.FailedType ?? "?", result.Reason ?? "?"));
    }

    /// <summary>
    ///     Shows the latest runs of an automation.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <example>.automationlog 3</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationLog(int id)
    {
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        if (automation is null)
        {
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
            return;
        }

        var runs = await Service.GetRunsAsync(ctx.Guild.Id, id, 15);
        if (runs.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutomationLogEmpty(ctx.Guild.Id, automation.Name));
            return;
        }

        await ctx.Channel.SendMessageAsync(
            embed: AutomationDisplay.Log(Strings, ctx.Guild.Id, automation, runs).Build());
    }

    /// <summary>
    ///     Lists the starter templates.
    /// </summary>
    /// <example>.automationtemplates</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationTemplates() =>
        await ctx.Channel.SendMessageAsync(embed: AutomationDisplay.Templates(Strings, ctx.Guild.Id).Build());

    /// <summary>
    ///     Creates automations from a starter template. Mention the channel, role or member it needs.
    /// </summary>
    /// <param name="template">The template ID.</param>
    /// <param name="targets">The channel, role or member mentions the template needs.</param>
    /// <example>.automationtemplate welcome #general</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AutomationTemplate(string template, [Remainder] string? targets = null)
    {
        var values = new Dictionary<string, ulong>();
        foreach (var tag in ctx.Message.Tags)
        {
            switch (tag.Type)
            {
                case TagType.ChannelMention:
                    values.TryAdd("channel", tag.Key);
                    break;
                case TagType.RoleMention:
                    values.TryAdd("role", tag.Key);
                    break;
                case TagType.UserMention:
                    values.TryAdd("user", tag.Key);
                    break;
            }
        }

        var outcome = await Service.CreateFromTemplateAsync(template, (SocketGuild)ctx.Guild, ctx.User.Id, values);
        if (outcome.Error != AutomationTemplateError.None)
        {
            await ReplyErrorAsync(AutomationDisplay.TemplateError(Strings, ctx.Guild.Id, outcome));
            return;
        }

        await ReplyConfirmAsync(Strings.AutomationTemplateCreated(ctx.Guild.Id,
            string.Join(", ", outcome.Created.Select(x => x.Name))));

        if (outcome.SetupError is not null)
            await ReplyErrorAsync(Strings.AutomationTemplateSetupFailed(ctx.Guild.Id, outcome.SetupError));
    }
}
