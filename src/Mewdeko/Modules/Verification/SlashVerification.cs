using System.IO;
using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Verification.Services;

namespace Mewdeko.Modules.Verification;

/// <summary>
///     Slash commands and buttons for making new members pass a captcha before they get access.
/// </summary>
[Group("verification", "Make new members pass a captcha before they get access")]
public class SlashVerification : MewdekoSlashModuleBase<VerificationService>
{
    private const string EnterButtonId = "verification_enter";
    private const string NewImageButtonId = "verification_new";
    private const string CodeModalId = "verification_code";

    /// <summary>
    ///     Shows the verification settings.
    /// </summary>
    [SlashCommand("show", "Shows the verification settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Show()
    {
        var setting = await Service.GetSettingsAsync(ctx.Guild.Id);
        await ctx.Interaction.RespondAsync(embed: Service.BuildSummary(ctx.Guild.Id, setting).Build(),
            ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Creates or uses a verification channel and posts the button in it.
    /// </summary>
    /// <param name="channel">The channel to use. A new one is created when empty.</param>
    [SlashCommand("setup", "Creates or uses a verification channel and posts the button")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Setup([ChannelTypes(ChannelType.Text)] ITextChannel? channel = null)
    {
        await DeferAsync(true);
        var result = await Service.SetupAsync(ctx.Guild, channel);
        if (result is null)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationSetupFailed(ctx.Guild.Id),
                Config);
            return;
        }

        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
            Strings.VerificationSetupDone(ctx.Guild.Id, result.Mention, "/verification"));
    }

    /// <summary>
    ///     Turns the gate on.
    /// </summary>
    [SlashCommand("enable", "Turns the verification gate on")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Enable() =>
        await ApplyAsync(x => x.Enabled = true, _ => Strings.VerificationEnabled(ctx.Guild.Id, "/verification"));

    /// <summary>
    ///     Turns the gate off and puts back the permissions it changed.
    /// </summary>
    [SlashCommand("disable", "Turns the gate off and puts back the permissions it changed")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Disable()
    {
        await DeferAsync(true);
        var (_, _, restored) = await Service.UpdateAsync(ctx.Guild.Id, x => x.Enabled = false);
        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.VerificationDisabled(ctx.Guild.Id, restored));
    }

    /// <summary>
    ///     Sets the roles used by the gate.
    /// </summary>
    /// <param name="unverified">The role given on join and removed when a member passes.</param>
    /// <param name="verified">The role given when a member passes.</param>
    [SlashCommand("roles", "Sets the unverified role and the verified role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Roles(IRole? unverified = null, IRole? verified = null)
    {
        await ApplyAsync(x =>
        {
            if (unverified is not null)
                x.UnverifiedRoleId = unverified.Id;
            if (verified is not null)
                x.VerifiedRoleId = verified.Id;
        }, _ => Strings.VerificationRolesSaved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the captcha rules.
    /// </summary>
    /// <param name="attempts">How many wrong answers a member gets, from 1 to 10.</param>
    /// <param name="timeout">How many minutes a captcha is valid and a failed member waits, from 1 to 60.</param>
    /// <param name="kickOnFail">Whether a member who fails is kicked.</param>
    [SlashCommand("rules", "Sets the attempts limit, timeout and kick on fail")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Rules(int? attempts = null, int? timeout = null, bool? kickOnFail = null)
    {
        await ApplyAsync(x =>
        {
            x.MaxAttempts = attempts ?? x.MaxAttempts;
            x.TimeoutMinutes = timeout ?? x.TimeoutMinutes;
            x.KickOnFail = kickOnFail ?? x.KickOnFail;
        }, _ => Strings.VerificationRulesSaved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the channel results are logged to.
    /// </summary>
    /// <param name="channel">The log channel. Leave empty to turn logging off.</param>
    [SlashCommand("log", "Sets the channel results are logged to")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Log([ChannelTypes(ChannelType.Text)] ITextChannel? channel = null) =>
        await ApplyAsync(x => x.LogChannelId = channel?.Id,
            _ => channel is null
                ? Strings.VerificationLogChannelOff(ctx.Guild.Id)
                : Strings.VerificationLogChannelSet(ctx.Guild.Id, channel.Mention));

    /// <summary>
    ///     Posts the verification button again.
    /// </summary>
    [SlashCommand("panel", "Posts the verification button again")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Panel()
    {
        await DeferAsync(true);
        if (await Service.PostPanelAsync(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.VerificationPanelPosted(ctx.Guild.Id));
            return;
        }

        await ctx.Interaction.SendEphemeralFollowupErrorAsync(
            Strings.VerificationNoChannel(ctx.Guild.Id, "/verification"), Config);
    }

    /// <summary>
    ///     Shows the channel permission changes and applies them after confirmation.
    /// </summary>
    [SlashCommand("permissions", "Shows the channel permission changes and applies them after you confirm")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Permissions()
    {
        await DeferAsync(true);
        var plan = await Service.PlanPermissionsAsync(ctx.Guild.Id);
        if (plan.Count == 0)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(
                Strings.VerificationNothingToChange(ctx.Guild.Id, "/verification"), Config);
            return;
        }

        if (!await PromptUserConfirmAsync(new EmbedBuilder().WithDescription(Service.DescribePlan(ctx.Guild.Id, plan)),
                ctx.User.Id, true))
            return;

        var (changed, failed) = await Service.ApplyPermissionsAsync(ctx.Guild.Id);
        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
            Strings.VerificationPermissionsApplied(ctx.Guild.Id, changed, failed));
    }

    /// <summary>
    ///     Verifies the members already in the server without a captcha.
    /// </summary>
    [SlashCommand("verify-existing", "Verifies the members already in the server without a captcha")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task VerifyExisting()
    {
        await DeferAsync(true);
        var setting = await Service.GetSettingsAsync(ctx.Guild.Id);
        if (setting.UnverifiedRoleId is null && setting.VerifiedRoleId is null)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationMissingRoles(ctx.Guild.Id),
                Config);
            return;
        }

        var count = await Service.CountExistingAsync(ctx.Guild.Id);
        if (count == 0)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationNobodyToVerify(ctx.Guild.Id),
                Config);
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.VerificationConfirmBulk(ctx.Guild.Id, count), ctx.User.Id, true))
            return;

        var started = await Service.VerifyExistingAsync(ctx.Guild.Id);
        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
            Strings.VerificationBulkStarted(ctx.Guild.Id, started ?? 0));
    }

    /// <summary>
    ///     Draws a captcha for the member who pressed the verification button.
    /// </summary>
    [ComponentInteraction(VerificationService.StartButtonId, true)]
    [RequireContext(ContextType.Guild)]
    public async Task StartVerification()
    {
        var result = await Service.StartAsync((IGuildUser)ctx.User);
        var guildId = ctx.Guild.Id;
        switch (result.Result)
        {
            case VerificationStart.Disabled:
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.VerificationGateOff(guildId), Config);
                return;
            case VerificationStart.AlreadyVerified:
                await ctx.Interaction.SendEphemeralConfirmAsync(Strings.VerificationAlready(guildId));
                return;
            case VerificationStart.Locked:
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.VerificationLocked(guildId, result.Minutes),
                    Config);
                return;
        }

        using var stream = new MemoryStream(result.Image!);
        await ctx.Interaction.RespondWithFileAsync(new FileAttachment(stream, "captcha.png"),
            embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.VerificationCaptchaPrompt(guildId, result.AttemptsLeft, result.Minutes))
                .WithImageUrl("attachment://captcha.png")
                .Build(),
            components: new ComponentBuilder()
                .WithButton(Strings.VerificationEnterButton(guildId), EnterButtonId, ButtonStyle.Success)
                .WithButton(Strings.VerificationNewImageButton(guildId), NewImageButtonId, ButtonStyle.Secondary)
                .Build(),
            ephemeral: true);
    }

    /// <summary>
    ///     Draws a new captcha for the member.
    /// </summary>
    [ComponentInteraction(NewImageButtonId, true)]
    [RequireContext(ContextType.Guild)]
    public Task NewImage() => StartVerification();

    /// <summary>
    ///     Opens the box for typing the code.
    /// </summary>
    [ComponentInteraction(EnterButtonId, true)]
    [RequireContext(ContextType.Guild)]
    public Task EnterCode() => ctx.Interaction.RespondWithModalAsync<VerificationCodeModal>(CodeModalId);

    /// <summary>
    ///     Checks the code the member typed.
    /// </summary>
    /// <param name="modal">The modal with the code.</param>
    [ModalInteraction(CodeModalId, true)]
    [RequireContext(ContextType.Guild)]
    public async Task CodeSubmitted(VerificationCodeModal modal)
    {
        await DeferAsync(true);
        var guildId = ctx.Guild.Id;
        var (outcome, attemptsLeft, minutes) = await Service.SubmitAsync((IGuildUser)ctx.User, modal.Code);
        switch (outcome)
        {
            case VerificationOutcome.Passed:
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.VerificationPassed(guildId));
                break;
            case VerificationOutcome.Wrong:
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationWrong(guildId, attemptsLeft),
                    Config);
                break;
            case VerificationOutcome.Locked:
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationLocked(guildId, minutes),
                    Config);
                break;
            case VerificationOutcome.Expired:
            case VerificationOutcome.NoChallenge:
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationExpired(guildId), Config);
                break;
            case VerificationOutcome.RoleError:
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationRoleError(guildId), Config);
                break;
            case VerificationOutcome.Disabled:
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VerificationGateOff(guildId), Config);
                break;
        }
    }

    private async Task ApplyAsync(Action<VerificationSetting> change, Func<VerificationSetting, string> success)
    {
        var (error, setting, _) = await Service.UpdateAsync(ctx.Guild.Id, change);
        if (error == VerificationError.None)
            await ctx.Interaction.SendEphemeralConfirmAsync(success(setting));
        else
            await ctx.Interaction.SendEphemeralErrorAsync(Service.DescribeError(ctx.Guild.Id, error), Config);
    }
}
