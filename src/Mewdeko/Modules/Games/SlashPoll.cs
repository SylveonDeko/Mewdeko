using System.Text;
using System.Text.Json;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;
using Embed = Discord.Embed;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games;

/// <summary>
/// Slash commands for creating and managing polls.
/// </summary>
[Group("poll", "Create and manage polls")]
public class SlashPoll : MewdekoSlashModuleBase<PollService>
{
    private readonly ILogger<SlashPoll> logger;
    private readonly PollTemplateService templateService;

    /// <summary>
    /// Initializes a new instance of the SlashPoll class.
    /// </summary>
    /// <param name="templateService">The template service.</param>
    /// <param name="logger">The logger instance.</param>
    public SlashPoll(PollTemplateService templateService, ILogger<SlashPoll> logger)
    {
        this.templateService = templateService;
        this.logger = logger;
    }

    /// <summary>
    /// Creates a simple yes/no poll.
    /// </summary>
    /// <param name="question">The poll question.</param>
    [SlashCommand("yesno", "Create a simple yes/no poll")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task YesNo([Summary("question", "The poll question")] string question)
    {
        await DeferAsync();

        try
        {
            var options = new List<PollOptionData>
            {
                new()
                {
                    Text = Strings.Yes(ctx.Guild.Id), Emote = "✅"
                },
                new()
                {
                    Text = Strings.No(ctx.Guild.Id), Emote = "❌"
                }
            };

            var settings = new PollSettings
            {
                AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            await CreatePollInternal(question, options, PollType.YesNo, settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create yes/no poll in guild {GuildId}", ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollCreationFailed(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Creates a custom poll with up to 10 options.
    /// </summary>
    /// <param name="question">The poll question.</param>
    /// <param name="option1">First poll option.</param>
    /// <param name="option2">Second poll option.</param>
    /// <param name="option3">Third poll option (optional).</param>
    /// <param name="option4">Fourth poll option (optional).</param>
    /// <param name="option5">Fifth poll option (optional).</param>
    /// <param name="option6">Sixth poll option (optional).</param>
    /// <param name="option7">Seventh poll option (optional).</param>
    /// <param name="option8">Eighth poll option (optional).</param>
    /// <param name="option9">Ninth poll option (optional).</param>
    /// <param name="option10">Tenth poll option (optional).</param>
    /// <param name="anonymous">Whether the poll should be anonymous.</param>
    /// <param name="multipleChoice">Whether users can select multiple options.</param>
    [SlashCommand("create", "Create a custom poll with multiple options")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task Create(
        [Summary("question", "The poll question")]
        string question,
        [Summary("option1", "First poll option")]
        string option1,
        [Summary("option2", "Second poll option")]
        string option2,
        [Summary("option3", "Third poll option")]
        string? option3 = null,
        [Summary("option4", "Fourth poll option")]
        string? option4 = null,
        [Summary("option5", "Fifth poll option")]
        string? option5 = null,
        [Summary("option6", "Sixth poll option")]
        string? option6 = null,
        [Summary("option7", "Seventh poll option")]
        string? option7 = null,
        [Summary("option8", "Eighth poll option")]
        string? option8 = null,
        [Summary("option9", "Ninth poll option")]
        string? option9 = null,
        [Summary("option10", "Tenth poll option")]
        string? option10 = null,
        [Summary("anonymous", "Whether the poll should be anonymous")]
        bool anonymous = false,
        [Summary("multiple-choice", "Whether users can select multiple options")]
        bool multipleChoice = false)
    {
        await DeferAsync();

        try
        {
            var optionStrings = new[]
                {
                    option1, option2, option3, option4, option5, option6, option7, option8, option9, option10
                }
                .Where(opt => !string.IsNullOrWhiteSpace(opt))
                .ToList();

            if (optionStrings.Count < 2)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollMinimumOptions(ctx.Guild.Id), Config);
                return;
            }

            var options = optionStrings.Select(opt => new PollOptionData
            {
                Text = opt!
            }).ToList();

            var pollType = anonymous ? PollType.Anonymous :
                multipleChoice ? PollType.MultiChoice : PollType.SingleChoice;

            var settings = new PollSettings
            {
                IsAnonymous = anonymous,
                AllowMultipleVotes = multipleChoice,
                AllowVoteChanges = true,
                ShowResults = true,
                ShowProgressBars = true
            };

            await CreatePollInternal(question, options, pollType, settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create custom poll in guild {GuildId}", ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollCreationFailed(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Creates a poll from a template.
    /// </summary>
    /// <param name="templateName">The name of the template to use.</param>
    [SlashCommand("template", "Create a poll from a template")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task Template([Summary("name", "The template name")] string templateName)
    {
        await DeferAsync();

        try
        {
            var template = await templateService.GetTemplateByNameAsync(ctx.Guild.Id, templateName);
            if (template == null)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateNotFound(ctx.Guild.Id), Config);
                return;
            }

            var options = JsonSerializer.Deserialize<List<PollOptionData>>(template.Options);
            if (options == null || options.Count == 0)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateInvalid(ctx.Guild.Id), Config);
                return;
            }

            PollSettings? settings = null;
            if (!string.IsNullOrEmpty(template.Settings))
            {
                settings = JsonSerializer.Deserialize<PollSettings>(template.Settings);
            }

            settings ??= new PollSettings
            {
                AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            var pollType = settings.IsAnonymous ? PollType.Anonymous :
                settings.AllowMultipleVotes ? PollType.MultiChoice : PollType.SingleChoice;

            await CreatePollInternal(template.Question, options, pollType, settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create poll from template '{TemplateName}' in guild {GuildId}", templateName,
                ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollCreationFailed(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Lists all available poll templates.
    /// </summary>
    [SlashCommand("templates", "List all available poll templates")]
    [CheckPermissions]
    public async Task Templates()
    {
        await DeferAsync();

        try
        {
            var templates = await templateService.GetTemplatesAsync(ctx.Guild.Id);

            if (templates.Count == 0)
            {
                await ctx.Interaction.FollowupAsync(Strings.PollNoTemplates(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollTemplatesTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .WithDescription(Strings.PollTemplatesFound(ctx.Guild.Id, templates.Count))
                .WithCurrentTimestamp();

            foreach (var template in templates.Take(10)) // Show first 10
            {
                var optionsJson = template.Options;
                var optionCount = 0;
                try
                {
                    var options = JsonSerializer.Deserialize<List<PollOptionData>>(optionsJson);
                    optionCount = options?.Count ?? 0;
                }
                catch
                {
                    // Ignore JSON parsing errors
                }

                var fieldValue = $"{Strings.PollTemplateQuestionField(ctx.Guild.Id)} {template.Question}\n" +
                                 $"{Strings.PollTemplateOptionsField(ctx.Guild.Id)} {optionCount}\n" +
                                 $"{Strings.PollTemplateCreatedField(ctx.Guild.Id)} {template.CreatedAt:yyyy-MM-dd}";

                embed.AddField(template.Name, fieldValue, true);
            }

            if (templates.Count > 10)
                embed.WithFooter(Strings.PollShowingTemplates(ctx.Guild.Id, templates.Count));

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list poll templates in guild {GuildId}", ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplatesError(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Ends a poll automatically once it has this many votes.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="votes">The number of votes, or 0 for no limit.</param>
    [SlashCommand("max-votes", "End a poll automatically after a number of votes")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task MaxVotes([Summary("poll-id", "The ID of the poll")] int pollId,
        [Summary("votes", "The number of votes, 0 removes the limit")] [MinValue(0)]
        int votes)
    {
        if (!await OwnsActivePollAsync(pollId) || votes < 0 ||
            !await Service.UpdatePollSettingsAsync(pollId, x => x.MaxVotes = votes == 0 ? null : votes))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id), Config);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(votes == 0
            ? Strings.PollMaxVotesCleared(ctx.Guild.Id)
            : Strings.PollMaxVotesSet(ctx.Guild.Id, votes));
    }

    /// <summary>
    /// Stops a role from voting on a poll, or lets it vote again if it was blocked.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="role">The role to block or unblock.</param>
    [SlashCommand("block-role", "Stop a role from voting on a poll")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task BlockRole([Summary("poll-id", "The ID of the poll")] int pollId,
        [Summary("role", "The role to block or unblock")] IRole role)
    {
        var blocked = false;

        if (!await OwnsActivePollAsync(pollId) || !await Service.UpdatePollSettingsAsync(pollId, x =>
            {
                x.BlockedRoles ??= [];
                blocked = !x.BlockedRoles.Remove(role.Id);

                if (blocked)
                    x.BlockedRoles.Add(role.Id);
            }))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id), Config);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(blocked
            ? Strings.PollRoleBlocked(ctx.Guild.Id, role.Mention)
            : Strings.PollRoleUnblocked(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    /// Sets the message posted when a poll ends.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="message">The message with poll placeholders. Leave empty to clear it.</param>
    [SlashCommand("results-message", "Set the message posted when a poll ends")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task ResultsMessage([Summary("poll-id", "The ID of the poll")] int pollId,
        [Summary("message", "Uses %poll.winner%, %poll.votes%, %poll.results% and more, empty clears")]
        string? message = null)
    {
        var text = string.IsNullOrWhiteSpace(message) ? null : message;

        if (!await OwnsActivePollAsync(pollId) ||
            !await Service.UpdatePollSettingsAsync(pollId, x => x.ResultsMessage = text))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id), Config);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(text is null
            ? Strings.PollResultsMessageCleared(ctx.Guild.Id)
            : Strings.PollResultsMessageSet(ctx.Guild.Id));
    }

    private async Task<bool> OwnsActivePollAsync(int pollId)
    {
        var poll = await Service.GetPollAsync(pollId);
        return poll is { IsActive: true } && poll.GuildId == ctx.Guild.Id;
    }

    /// <summary>
    /// Closes an active poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll to close.</param>
    [SlashCommand("close", "Close an active poll")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task Close([Summary("poll-id", "The ID of the poll to close")] int pollId)
    {
        await DeferAsync();

        try
        {
            var poll = await Service.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != ctx.Guild.Id)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollNotFound(ctx.Guild.Id), Config);
                return;
            }

            if (!poll.IsActive)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollAlreadyClosed(ctx.Guild.Id), Config);
                return;
            }

            // Check if user has permission to close (creator or manage messages)
            if (poll.CreatorId != ctx.User.Id && !((IGuildUser)ctx.User).GuildPermissions.ManageMessages)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollManageNoPermission(ctx.Guild.Id), Config);
                return;
            }

            var success = await Service.ClosePollAsync(pollId, ctx.User.Id);
            if (!success)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollCloseFailed(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Interaction.FollowupAsync(Strings.PollCloseSuccess(ctx.Guild.Id, poll.Question));

            // Update the poll message
            await UpdatePollMessage(poll, Strings.PollStatusClosed(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to close poll {PollId} in guild {GuildId}", pollId, ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollCloseRetry(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Shows statistics for a poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll to show stats for.</param>
    [SlashCommand("stats", "Show statistics for a poll")]
    [CheckPermissions]
    public async Task Stats([Summary("poll-id", "The ID of the poll")] int pollId)
    {
        await DeferAsync();

        try
        {
            var poll = await Service.GetPollAsync(pollId);
            if (poll == null || poll.GuildId != ctx.Guild.Id)
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.PollNotFound(ctx.Guild.Id), Config);
                return;
            }

            var stats = await Service.GetPollStatsAsync(pollId);
            if (stats == null)
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.PollStatsError(ctx.Guild.Id), Config);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollStatsTitleWithQuestion(ctx.Guild.Id, poll.Question))
                .WithColor(poll.IsActive ? Color.Green : Color.Red)
                .WithCurrentTimestamp();

            embed.AddField(Strings.PollStatsTotalVotes(ctx.Guild.Id), stats.TotalVotes.ToString(), true)
                .AddField(Strings.PollStatsUniqueVoters(ctx.Guild.Id), stats.UniqueVoters.ToString(), true)
                .AddField(Strings.PollStatsStatus(ctx.Guild.Id),
                    poll.IsActive ? Strings.PollStatsActive(ctx.Guild.Id) : Strings.PollStatsClosed(ctx.Guild.Id),
                    true);

            if (stats.AverageVoteTime.TotalMinutes > 0)
            {
                embed.AddField(Strings.PollStatsAvgTime(ctx.Guild.Id),
                    stats.AverageVoteTime.TotalMinutes < 60
                        ? $"{stats.AverageVoteTime.TotalMinutes:F1} {Strings.Minutes(ctx.Guild.Id)}"
                        : $"{stats.AverageVoteTime.TotalHours:F1} {Strings.Hours(ctx.Guild.Id)}", true);
            }

            // Add option results
            var optionResults = new StringBuilder();
            foreach (var option in poll.PollOptions.OrderBy(o => o.Index))
            {
                var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
                var percentage = stats.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;

                var voteCountText = string.Format(Strings.PollVoteCountFormat(ctx.Guild.Id, option.Index + 1,
                    option.Text, voteCount, percentage));
                optionResults.AppendLine(voteCountText);
                optionResults.AppendLine();
            }

            if (optionResults.Length > 0)
                embed.AddField(Strings.PollStatsResults(ctx.Guild.Id), optionResults.ToString());

            embed.AddField(Strings.PollStatsCreated(ctx.Guild.Id),
                $"<t:{((DateTimeOffset)poll.CreatedAt).ToUnixTimeSeconds()}:R>", true);

            if (poll.ClosedAt.HasValue)
                embed.AddField(Strings.PollStatsClosedAt(ctx.Guild.Id),
                    $"<t:{((DateTimeOffset)poll.ClosedAt.Value).ToUnixTimeSeconds()}:R>", true);

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show stats for poll {PollId} in guild {GuildId}", pollId, ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollStatsRetry(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Lists active polls in the server.
    /// </summary>
    [SlashCommand("list", "List active polls in the server")]
    [CheckPermissions]
    public async Task List()
    {
        await DeferAsync();

        try
        {
            var polls = await Service.GetActivePollsAsync(ctx.Guild.Id);

            if (polls.Count == 0)
            {
                await ctx.Interaction.FollowupAsync(Strings.PollListNoActive(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollActiveTitle(ctx.Guild.Id))
                .WithColor(Color.Green)
                .WithDescription(Strings.PollActivePollsFound(ctx.Guild.Id, polls.Count))
                .WithCurrentTimestamp();

            foreach (var poll in polls.Take(10)) // Show first 10
            {
                var channel = await ctx.Guild.GetTextChannelAsync(poll.ChannelId);
                var channelMention = channel != null ? channel.Mention : $"<#{poll.ChannelId}>";

                var stats = await Service.GetPollStatsAsync(poll.Id);
                var voteCount = stats?.TotalVotes ?? 0;

                var fieldValue = $"{Strings.PollListChannelField(ctx.Guild.Id)} {channelMention}\n" +
                                 $"{Strings.PollListVotesField(ctx.Guild.Id)} {voteCount}\n" +
                                 $"{Strings.PollListCreatedField(ctx.Guild.Id)} <t:{((DateTimeOffset)poll.CreatedAt).ToUnixTimeSeconds()}:R>\n" +
                                 $"{Strings.PollListIdField(ctx.Guild.Id)} {poll.Id}";

                embed.AddField(poll.Question.Length > 50 ? poll.Question[..47] + "..." : poll.Question,
                    fieldValue, true);
            }

            if (polls.Count > 10)
                embed.WithFooter(Strings.PollShowingActive(ctx.Guild.Id, polls.Count));

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list active polls in guild {GuildId}", ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollListRetry(ctx.Guild.Id), Config);
        }
    }

    #region Template Management

    /// <summary>
    /// Creates a new poll template.
    /// </summary>
    /// <param name="name">The template name.</param>
    /// <param name="question">The template question.</param>
    /// <param name="options">The template options (separated by semicolons).</param>
    [SlashCommand("create-template", "Create a new poll template")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task CreateTemplate(
        [Summary("name", "The template name")] string name,
        [Summary("question", "The template question")]
        string question,
        [Summary("options", "Poll options separated by semicolons (;)")]
        string options)
    {
        await DeferAsync();

        try
        {
            var optionParts = options.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(opt => opt.Trim())
                .Where(opt => !string.IsNullOrWhiteSpace(opt))
                .ToList();

            switch (optionParts.Count)
            {
                case < 2:
                    await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateMinOptions(ctx.Guild.Id), Config);
                    return;
                case > 25:
                    await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateMaxOptions(ctx.Guild.Id), Config);
                    return;
            }

            var pollOptions = optionParts.Select(opt => new PollOptionData
            {
                Text = opt
            }).ToList();

            var settings = new PollSettings
            {
                AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            var template =
                await templateService.CreateTemplateAsync(ctx.Guild.Id, ctx.User.Id, name, question, pollOptions,
                    settings);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollTemplateCreatedTitle(ctx.Guild.Id))
                .WithColor(Color.Green)
                .WithDescription(Strings.PollTemplateCreatedDesc(ctx.Guild.Id, template.Name))
                .AddField(Strings.PollFieldQuestion(ctx.Guild.Id), template.Question)
                .AddField(Strings.PollFieldOptions(ctx.Guild.Id), string.Join(", ", optionParts))
                .WithCurrentTimestamp();

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }
        catch (InvalidOperationException ex)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(ex.Message, Config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create template '{TemplateName}' in guild {GuildId}", name, ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateCreateRetry(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    /// Deletes a poll template.
    /// </summary>
    /// <param name="templateName">The name of the template to delete.</param>
    [SlashCommand("delete-template", "Delete a poll template")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task DeleteTemplate([Summary("name", "The template name to delete")] string templateName)
    {
        await DeferAsync();

        try
        {
            var template = await templateService.GetTemplateByNameAsync(ctx.Guild.Id, templateName);
            if (template == null)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateNotFound(ctx.Guild.Id), Config);
                return;
            }

            // Check if user has permission to delete (creator or manage messages)
            if (template.CreatorId != ctx.User.Id && !((IGuildUser)ctx.User).GuildPermissions.ManageMessages)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateDeleteNoPermission(ctx.Guild.Id),
                    Config);
                return;
            }

            var success = await templateService.DeleteTemplateAsync(template.Id, ctx.User.Id);
            if (!success)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollDeleteFailed(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Interaction.FollowupAsync(Strings.PollTemplateDeleteSuccess(ctx.Guild.Id, templateName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete template '{TemplateName}' in guild {GuildId}", templateName,
                ctx.Guild.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollTemplateDeleteRetry(ctx.Guild.Id), Config);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Internal method to create a poll.
    /// </summary>
    /// <param name="question">The poll question.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <param name="settings">The poll settings.</param>
    private async Task CreatePollInternal(string question, List<PollOptionData> options, PollType pollType,
        PollSettings settings)
    {
        // Send the poll message first
        var embed = await BuildPollEmbed(question, options, pollType);
        var components = BuildPollComponents(0, options, pollType); // Temporary ID

        var message = await ctx.Channel.SendMessageAsync(embed: embed, components: components);

        // Create the poll in the database
        var poll = await Service.CreatePollAsync(ctx.Guild.Id, ctx.Channel.Id, message.Id,
            ctx.User.Id, question, options, pollType, settings);

        // Update the message with the correct poll ID
        var updatedEmbed = await BuildPollEmbed(question, options, pollType, poll.Id);
        var updatedComponents = BuildPollComponents(poll.Id, options, pollType);

        await message.ModifyAsync(msg =>
        {
            msg.Embed = updatedEmbed;
            msg.Components = updatedComponents;
        });

        await ctx.Interaction.FollowupAsync(Strings.PollCreateSuccess(ctx.Guild.Id, question, poll.Id));
    }

    /// <summary>
    /// Builds the poll embed for display.
    /// </summary>
    /// <param name="question">The poll question.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <param name="pollId">The poll ID (optional).</param>
    /// <returns>The built embed.</returns>
    private async Task<Embed> BuildPollEmbed(string question, List<PollOptionData> options, PollType pollType,
        int? pollId = null)
    {
        await Task.CompletedTask;

        var typeIcon = pollType switch
        {
            PollType.YesNo => "✅❌",
            PollType.SingleChoice => "📊",
            PollType.MultiChoice => "☑️",
            PollType.Anonymous => "🔒",
            PollType.RoleRestricted => "👥",
            _ => "📊"
        };

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PollTitleFormat(ctx.Guild.Id, typeIcon, question))
            .WithColor(GetPollColor(pollType))
            .WithTimestamp(DateTimeOffset.UtcNow);

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var optionText = $"{i + 1}. {option.Text}";
            if (!string.IsNullOrEmpty(option.Emote))
                optionText = $"{option.Emote} {optionText}";

            embed.AddField(optionText, Strings.PollInitialVoteCount(ctx.Guild.Id), true);
        }

        var footerText = pollType switch
        {
            PollType.MultiChoice => Strings.PollFooterMultipleChoice(ctx.Guild.Id),
            PollType.Anonymous => Strings.PollFooterAnonymous(ctx.Guild.Id),
            PollType.RoleRestricted => Strings.PollFooterRoleRestricted(ctx.Guild.Id),
            _ => Strings.PollFooterDefault(ctx.Guild.Id)
        };

        if (pollId.HasValue)
            footerText += $" • {Strings.PollIdLabel(ctx.Guild.Id)} {pollId.Value}";

        embed.WithFooter(footerText);

        return embed.Build();
    }

    /// <summary>
    /// Builds the interactive components for the poll.
    /// </summary>
    /// <param name="pollId">The poll ID.</param>
    /// <param name="options">The poll options.</param>
    /// <param name="pollType">The poll type.</param>
    /// <returns>The built message component.</returns>
    private MessageComponent BuildPollComponents(int pollId, List<PollOptionData> options, PollType pollType)
    {
        var builder = new ComponentBuilder();

        if (options.Count <= 5)
        {
            // Use buttons for 2-5 options
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var label = $"{i + 1}";
                if (option.Text.Length <= 12)
                    label = $"{i + 1}. {option.Text}";

                if (label.Length > 80) label = label[..77] + Strings.PollTruncated(ctx.Guild.Id);

                IEmote? emote = null;
                if (!string.IsNullOrEmpty(option.Emote))
                {
                    try
                    {
                        emote = Emote.Parse(option.Emote);
                    }
                    catch
                    {
                        emote = new Emoji(option.Emote);
                    }
                }

                var style = GetButtonStyle(i);
                builder.WithButton(label, $"poll:vote:{pollId}:{i}", style, emote);
            }
        }
        else
        {
            // Use select menu for 6+ options
            var selectMenuBuilder = new SelectMenuBuilder()
                .WithCustomId($"poll:select:{pollId}")
                .WithPlaceholder(Strings.PollSelectPlaceholder(ctx.Guild.Id))
                .WithMinValues(1)
                .WithMaxValues(pollType == PollType.MultiChoice ? Math.Min(options.Count, 25) : 1);

            for (var i = 0; i < Math.Min(options.Count, 25); i++)
            {
                var option = options[i];
                var label = $"{i + 1}. {option.Text}";
                if (label.Length > 100) label = label[..97] + Strings.PollTruncated(ctx.Guild.Id);

                IEmote? emote = null;
                if (!string.IsNullOrEmpty(option.Emote))
                {
                    try
                    {
                        emote = Emote.Parse(option.Emote);
                    }
                    catch
                    {
                        emote = new Emoji(option.Emote);
                    }
                }

                selectMenuBuilder.AddOption(label, i.ToString(), emote: emote);
            }

            builder.WithSelectMenu(selectMenuBuilder);
        }

        // Add management buttons on second row
        builder.WithButton(Strings.PollButtonStats(ctx.Guild.Id), $"poll:manage:{pollId}:stats", ButtonStyle.Secondary)
            .WithButton(Strings.PollButtonClose(ctx.Guild.Id), $"poll:manage:{pollId}:close", ButtonStyle.Secondary)
            .WithButton(Strings.PollButtonDelete(ctx.Guild.Id), $"poll:manage:{pollId}:delete", ButtonStyle.Danger);

        return builder.Build();
    }

    /// <summary>
    /// Gets the color for a poll type.
    /// </summary>
    /// <param name="pollType">The poll type.</param>
    /// <returns>The color for the poll type.</returns>
    private static Color GetPollColor(PollType pollType)
    {
        return pollType switch
        {
            PollType.YesNo => Color.Green,
            PollType.SingleChoice => Color.Blue,
            PollType.MultiChoice => Color.Purple,
            PollType.Anonymous => Color.DarkGrey,
            PollType.RoleRestricted => Color.Orange,
            _ => Color.Blue
        };
    }

    /// <summary>
    /// Gets the button style based on option index.
    /// </summary>
    /// <param name="index">The option index.</param>
    /// <returns>The button style.</returns>
    private static ButtonStyle GetButtonStyle(int index)
    {
        return index switch
        {
            0 => ButtonStyle.Primary,
            1 => ButtonStyle.Secondary,
            2 => ButtonStyle.Success,
            3 => ButtonStyle.Danger,
            _ => ButtonStyle.Secondary
        };
    }

    /// <summary>
    /// Updates the poll message with new status.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="statusMessage">The status message to display.</param>
    private async Task UpdatePollMessage(Poll poll, string statusMessage)
    {
        try
        {
            var channel = await ctx.Guild.GetTextChannelAsync(poll.ChannelId);

            if (await channel?.GetMessageAsync(poll.MessageId) is IUserMessage message)
            {
                var embed = message.Embeds.FirstOrDefault()?.ToEmbedBuilder()
                    .WithColor(Color.Red)
                    .WithFooter($"{statusMessage} • {Strings.PollIdLabel(ctx.Guild.Id)} {poll.Id}")
                    .Build();

                await message.ModifyAsync(msg =>
                {
                    msg.Embed = embed;
                    msg.Components = new ComponentBuilder().Build(); // Remove components
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update poll message for poll {PollId}", poll.Id);
        }
    }

    #endregion
}