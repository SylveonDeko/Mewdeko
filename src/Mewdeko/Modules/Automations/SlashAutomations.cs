using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Automations.Common;
using Mewdeko.Modules.Automations.Services;

namespace Mewdeko.Modules.Automations;

/// <summary>
///     The starter templates, as slash command choices.
/// </summary>
public enum AutomationTemplateChoice
{
    /// <summary>
    ///     Thanks members who start boosting.
    /// </summary>
    [ChoiceDisplay("Thank boosters")]
    ThankBoosters,

    /// <summary>
    ///     Welcomes new members.
    /// </summary>
    [ChoiceDisplay("Welcome message")]
    Welcome,

    /// <summary>
    ///     Adds a member to every new thread.
    /// </summary>
    [ChoiceDisplay("Thread joiner")]
    ThreadJoiner,

    /// <summary>
    ///     Reposts messages that get three stars.
    /// </summary>
    [ChoiceDisplay("Starboard repost")]
    Starboard,

    /// <summary>
    ///     Posts a button that toggles a role.
    /// </summary>
    [ChoiceDisplay("Button role toggle")]
    ButtonRole
}

/// <summary>
///     Slash commands for viewing and managing automations. Building them is done in the dashboard.
/// </summary>
[Group("automation", "View and manage automations")]
public class SlashAutomations : MewdekoSlashModuleBase<AutomationService>
{
    /// <summary>
    ///     Lists the automations of the server.
    /// </summary>
    [SlashCommand("list", "List the automations of this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task List()
    {
        var automations = await Service.GetAllAsync(ctx.Guild.Id);
        if (automations.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutomationNone(ctx.Guild.Id));
            return;
        }

        await RespondAsync(embed: AutomationDisplay.List(Strings, ctx.Guild.Id, automations).Build());
    }

    /// <summary>
    ///     Shows one automation.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    [SlashCommand("show", "Show one automation")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Show([Summary("id", "The automation ID")] int id)
    {
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        if (automation is null)
        {
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
            return;
        }

        await RespondAsync(embed: AutomationDisplay.Show(Strings, ctx.Guild.Id, automation).Build());
    }

    /// <summary>
    ///     Turns an automation on or off.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    /// <param name="enabled">Whether it should run.</param>
    [SlashCommand("toggle", "Turn an automation on or off")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Toggle([Summary("id", "The automation ID")] int id,
        [Summary("enabled", "Whether it should run")] bool enabled)
    {
        var automation = await Service.SetEnabledAsync(ctx.Guild.Id, id, enabled);
        if (automation is null)
            await ReplyErrorAsync(Strings.AutomationNotFound(ctx.Guild.Id, id));
        else if (enabled)
            await ReplyConfirmAsync(Strings.AutomationEnabled(ctx.Guild.Id, automation.Name));
        else
            await ReplyConfirmAsync(Strings.AutomationDisabled(ctx.Guild.Id, automation.Name));
    }

    /// <summary>
    ///     Deletes an automation.
    /// </summary>
    /// <param name="id">The automation ID.</param>
    [SlashCommand("delete", "Delete an automation")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Delete([Summary("id", "The automation ID")] int id)
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
    [SlashCommand("test", "Run an automation once as if you triggered it here")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Test([Summary("id", "The automation ID")] int id)
    {
        await DeferAsync();
        var automation = await Service.GetAsync(ctx.Guild.Id, id);
        var result = automation is null
            ? null
            : await Service.RunTestAsync((SocketGuild)ctx.Guild, id, (IGuildUser)ctx.User, ctx.Channel, null);

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
    [SlashCommand("log", "Show the latest runs of an automation")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Log([Summary("id", "The automation ID")] int id)
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

        await RespondAsync(embed: AutomationDisplay.Log(Strings, ctx.Guild.Id, automation, runs).Build());
    }

    /// <summary>
    ///     Lists the starter templates.
    /// </summary>
    [SlashCommand("templates", "List the starter templates")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public Task Templates() => RespondAsync(embed: AutomationDisplay.Templates(Strings, ctx.Guild.Id).Build());

    /// <summary>
    ///     Creates automations from a starter template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="channel">The channel the template needs.</param>
    /// <param name="role">The role the template needs.</param>
    /// <param name="user">The member the template needs.</param>
    [SlashCommand("template", "Create automations from a starter template")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Template([Summary("template", "The template")] AutomationTemplateChoice template,
        [Summary("channel", "The channel the template needs")] IGuildChannel? channel = null,
        [Summary("role", "The role the template needs")] IRole? role = null,
        [Summary("user", "The member the template needs")] IUser? user = null)
    {
        await DeferAsync();
        var values = new Dictionary<string, ulong>();
        if (channel is not null)
            values["channel"] = channel.Id;
        if (role is not null)
            values["role"] = role.Id;
        if (user is not null)
            values["user"] = user.Id;

        var id = template switch
        {
            AutomationTemplateChoice.ThankBoosters => "thank-boosters",
            AutomationTemplateChoice.Welcome => "welcome",
            AutomationTemplateChoice.ThreadJoiner => "thread-joiner",
            AutomationTemplateChoice.Starboard => "starboard",
            _ => "button-role"
        };

        var outcome = await Service.CreateFromTemplateAsync(id, (SocketGuild)ctx.Guild, ctx.User.Id, values);
        if (outcome.Error != AutomationTemplateError.None)
        {
            await ReplyErrorAsync(AutomationDisplay.TemplateError(Strings, ctx.Guild.Id, outcome));
            return;
        }

        await ReplyConfirmAsync(Strings.AutomationTemplateCreated(ctx.Guild.Id,
            string.Join(", ", outcome.Created.Select(x => x.Name))));

        if (outcome.SetupError is not null)
            await FollowupAsync(Strings.AutomationTemplateSetupFailed(ctx.Guild.Id, outcome.SetupError));
    }
}
