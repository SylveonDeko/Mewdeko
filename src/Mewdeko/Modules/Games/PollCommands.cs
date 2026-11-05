using System.Text.Json;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Games;

/// <summary>
/// Text commands for creating and managing polls.
/// </summary>
public class PollCommands : MewdekoModuleBase<PollService>
{
    private readonly ILogger<PollCommands> logger;
    private readonly PollTemplateService templateService;

    /// <summary>
    /// Initializes a new instance of the PollCommands class.
    /// </summary>
    /// <param name="templateService">The template service.</param>
    /// <param name="logger">The logger instance.</param>
    public PollCommands(PollTemplateService templateService, ILogger<PollCommands> logger)
    {
        this.templateService = templateService;
        this.logger = logger;
    }

    /// <summary>
    /// Creates a simple yes/no poll.
    /// </summary>
    /// <param name="question">The poll question.</param>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task Poll([Remainder] string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            await ReplyErrorAsync("poll_question_required");
            return;
        }

        try
        {
            var options = new List<PollOptionData>
            {
                new()
                {
                    Text = "Yes", Emote = "✅"
                },
                new()
                {
                    Text = "No", Emote = "❌"
                }
            };

            var settings = new PollSettings
            {
                AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            // Send the poll message first
            var embed = await BuildPollEmbed(question, options, PollType.YesNo);
            var components = BuildPollComponents(0, options, PollType.YesNo); // Temporary ID

            var message = await ctx.Channel.SendMessageAsync(embed: embed, components: components);

            // Create the poll in the database
            var poll = await Service.CreatePollAsync(ctx.Guild.Id, ctx.Channel.Id, message.Id,
                ctx.User.Id, question, options, PollType.YesNo, settings);

            // Update the message with the correct poll ID
            var updatedEmbed = await BuildPollEmbed(question, options, PollType.YesNo, poll.Id);
            var updatedComponents = BuildPollComponents(poll.Id, options, PollType.YesNo);

            await message.ModifyAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = updatedComponents;
            });

            // Delete the command message
            await ctx.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create yes/no poll in guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync("poll_creation_failed");
        }
    }

    /// <summary>
    /// Creates a custom poll with multiple options.
    /// </summary>
    /// <param name="input">The poll input in format: "question;option1;option2;..."</param>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task Pollc([Remainder] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyErrorAsync("poll_input_required");
            return;
        }

        var parts = input.Split(';');
        switch (parts.Length)
        {
            case < 3:
                await ReplyErrorAsync("poll_minimum_options");
                return;
            // Question + 25 options max
            case > 26:
                await ReplyErrorAsync("poll_maximum_options");
                return;
            default:
                try
                {
                    var question = parts[0].Trim();
                    var options = new List<PollOptionData>();

                    for (var i = 1; i < parts.Length; i++)
                    {
                        var optionText = parts[i].Trim();
                        if (!string.IsNullOrWhiteSpace(optionText))
                        {
                            options.Add(new PollOptionData
                            {
                                Text = optionText
                            });
                        }
                    }

                    if (options.Count < 2)
                    {
                        await ReplyErrorAsync("poll_minimum_options");
                        return;
                    }

                    var settings = new PollSettings
                    {
                        AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
                    };

                    var pollType = PollType.SingleChoice;

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

                    // Delete the command message
                    await ctx.Message.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create custom poll in guild {GuildId}", ctx.Guild.Id);
                    await ReplyErrorAsync("poll_creation_failed");
                }

                break;
        }
    }

    /// <summary>
    /// Creates a multi-choice poll where users can select multiple options.
    /// </summary>
    /// <param name="input">The poll input in format: "question;option1;option2;..."</param>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task Pollm([Remainder] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyErrorAsync("poll_input_required");
            return;
        }

        var parts = input.Split(';');
        if (parts.Length < 3)
        {
            await ReplyErrorAsync("poll_minimum_options");
            return;
        }

        try
        {
            var question = parts[0].Trim();
            var options = new List<PollOptionData>();

            for (var i = 1; i < parts.Length; i++)
            {
                var optionText = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(optionText))
                {
                    options.Add(new PollOptionData
                    {
                        Text = optionText
                    });
                }
            }

            var settings = new PollSettings
            {
                AllowMultipleVotes = true, AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            var pollType = PollType.MultiChoice;

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

            // Delete the command message
            await ctx.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create multi-choice poll in guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync("poll_creation_failed");
        }
    }

    /// <summary>
    /// Creates an anonymous poll where voter identities are hidden.
    /// </summary>
    /// <param name="input">The poll input in format: "question;option1;option2;..."</param>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task Polla([Remainder] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyErrorAsync("poll_input_required");
            return;
        }

        var parts = input.Split(';');
        if (parts.Length < 3)
        {
            await ReplyErrorAsync("poll_minimum_options");
            return;
        }

        try
        {
            var question = parts[0].Trim();
            var options = new List<PollOptionData>();

            for (var i = 1; i < parts.Length; i++)
            {
                var optionText = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(optionText))
                {
                    options.Add(new PollOptionData
                    {
                        Text = optionText
                    });
                }
            }

            var settings = new PollSettings
            {
                IsAnonymous = true, AllowVoteChanges = true, ShowResults = true, ShowProgressBars = true
            };

            var pollType = PollType.Anonymous;

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

            // Delete the command message
            await ctx.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create anonymous poll in guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync("poll_creation_failed");
        }
    }

    /// <summary>
    /// Lists all poll templates available in the guild.
    /// </summary>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task PollTemplates()
    {
        try
        {
            var templates = await templateService.GetTemplatesAsync(ctx.Guild.Id);

            if (templates.Count == 0)
            {
                await ReplyErrorAsync("poll_no_templates");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollTemplatesTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .WithDescription($"Found {templates.Count} template(s)");

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

                embed.AddField(template.Name,
                    $"Question: {template.Question}\nOptions: {optionCount}\nCreated: {template.CreatedAt:yyyy-MM-dd}",
                    true);
            }

            if (templates.Count > 10)
                embed.WithFooter(Strings.PollShowingTemplates(ctx.Guild.Id, templates.Count));

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list poll templates in guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync("poll_templates_error");
        }
    }

    /// <summary>
    /// Creates a poll from a template.
    /// </summary>
    /// <param name="templateName">The name of the template to use.</param>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task PollTemplate([Remainder] string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            await ReplyErrorAsync("poll_template_name_required");
            return;
        }

        try
        {
            var template = await templateService.GetTemplateByNameAsync(ctx.Guild.Id, templateName);
            if (template == null)
            {
                await ReplyErrorAsync("poll_template_not_found");
                return;
            }

            var options = JsonSerializer.Deserialize<List<PollOptionData>>(template.Options);
            if (options == null || options.Count == 0)
            {
                await ReplyErrorAsync("poll_template_invalid");
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

            var pollType = PollType.SingleChoice; // Default type

            // Send the poll message first
            var embed = await BuildPollEmbed(template.Question, options, pollType);
            var components = BuildPollComponents(0, options, pollType); // Temporary ID

            var message = await ctx.Channel.SendMessageAsync(embed: embed, components: components);

            // Create the poll in the database
            var poll = await Service.CreatePollAsync(ctx.Guild.Id, ctx.Channel.Id, message.Id,
                ctx.User.Id, template.Question, options, pollType, settings);

            // Update the message with the correct poll ID
            var updatedEmbed = await BuildPollEmbed(template.Question, options, pollType, poll.Id);
            var updatedComponents = BuildPollComponents(poll.Id, options, pollType);

            await message.ModifyAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = updatedComponents;
            });

            // Delete the command message
            await ctx.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create poll from template '{TemplateName}' in guild {GuildId}", templateName,
                ctx.Guild.Id);
            await ReplyErrorAsync("poll_creation_failed");
        }
    }

    #region Private Methods

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

            embed.AddField(optionText, "0 votes (0%)", true);
        }

        var footerText = pollType switch
        {
            PollType.MultiChoice => Strings.PollFooterMultipleChoice(ctx.Guild.Id),
            PollType.Anonymous => Strings.PollFooterAnonymous(ctx.Guild.Id),
            PollType.RoleRestricted => Strings.PollFooterRoleRestricted(ctx.Guild.Id),
            _ => Strings.PollFooterDefault(ctx.Guild.Id)
        };

        if (pollId.HasValue)
            footerText += $" • Poll ID: {pollId.Value}";

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

                if (label.Length > 80) label = label[..77] + "...";

                IEmote? emote = null;
                if (!string.IsNullOrEmpty(option.Emote))
                {
                    try
                    {
                        emote = Emote.Parse(option.Emote);
                    }
                    catch
                    {
                        // If parsing fails, try as unicode emoji
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
                if (label.Length > 100) label = label[..97] + "...";

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
        builder.WithButton("📊 Stats", $"poll:manage:{pollId}:stats", ButtonStyle.Secondary)
            .WithButton("🔒 Close", $"poll:manage:{pollId}:close", ButtonStyle.Secondary)
            .WithButton("🗑️ Delete", $"poll:manage:{pollId}:delete", ButtonStyle.Danger);

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

    #endregion

    /// <summary>
    /// Ends a poll automatically once it has this many votes. Use 0 to remove the limit.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="votes">The number of votes, or 0 for no limit.</param>
    /// <example>.pollmaxvotes 4 50</example>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task PollMaxVotes(int pollId, int votes)
    {
        if (!await OwnsActivePollAsync(pollId) || votes < 0 ||
            !await Service.UpdatePollSettingsAsync(pollId, x => x.MaxVotes = votes == 0 ? null : votes))
        {
            await ErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(votes == 0
            ? Strings.PollMaxVotesCleared(ctx.Guild.Id)
            : Strings.PollMaxVotesSet(ctx.Guild.Id, votes));
    }

    /// <summary>
    /// Stops a role from voting on a poll, or lets it vote again if it was blocked.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="role">The role to block or unblock.</param>
    /// <example>.pollblockrole 4 @Muted</example>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task PollBlockRole(int pollId, IRole role)
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
            await ErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(blocked
            ? Strings.PollRoleBlocked(ctx.Guild.Id, role.Mention)
            : Strings.PollRoleUnblocked(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    /// Sets the message posted when a poll ends. Leave it out to clear it.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="message">The message. Uses %poll.question%, %poll.votes%, %poll.voters%, %poll.winner%, %poll.winner.votes%, %poll.results%, %poll.id% and %poll.link%.</param>
    /// <example>.pollresults 4 The winner is %poll.winner% with %poll.winner.votes% votes!</example>
    [Cmd, Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task PollResultsMessage(int pollId, [Remainder] string? message = null)
    {
        var text = string.IsNullOrWhiteSpace(message) ? null : message;

        if (!await OwnsActivePollAsync(pollId) ||
            !await Service.UpdatePollSettingsAsync(pollId, x => x.ResultsMessage = text))
        {
            await ErrorAsync(Strings.PollSettingsFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(text is null
            ? Strings.PollResultsMessageCleared(ctx.Guild.Id)
            : Strings.PollResultsMessageSet(ctx.Guild.Id));
    }

    private async Task<bool> OwnsActivePollAsync(int pollId)
    {
        var poll = await Service.GetPollAsync(pollId);
        return poll is { IsActive: true } && poll.GuildId == ctx.Guild.Id;
    }
}