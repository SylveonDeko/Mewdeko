using Discord.Interactions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Games.Common;
using Embed = Discord.Embed;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Handles Discord component interactions for poll voting.
/// </summary>
public class PollButtonService : MewdekoSlashModuleBase<PollService>
{
    private readonly BotConfig config;
    private readonly ILogger<PollButtonService> logger;

    /// <summary>
    /// Initializes a new instance of the PollButtonService class.
    /// </summary>
    /// <param name="config">The bot configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public PollButtonService(BotConfig config, ILogger<PollButtonService> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// Processes button interactions for poll voting.
    /// </summary>
    /// <param name="pollId">The ID of the poll being voted on.</param>
    /// <param name="optionIndex">The index of the selected option.</param>
    [ComponentInteraction("poll:vote:*:*")]
    public async Task HandlePollVote(string pollId, string optionIndex)
    {
        if (!int.TryParse(pollId, out var pollIdInt) || !int.TryParse(optionIndex, out var optionIndexInt))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollInvalidOption(ctx.Guild.Id), config);
            return;
        }

        try
        {
            var (success, result) = await Service.ProcessVoteAsync(pollIdInt, ctx.User.Id, [
                optionIndexInt
            ]);

            var message = result switch
            {
                VoteResult.Success => Strings.PollVoteSuccess(ctx.Guild.Id),
                VoteResult.Changed => Strings.PollVoteChanged(ctx.Guild.Id),
                VoteResult.Removed => Strings.PollVoteRemoved(ctx.Guild.Id),
                VoteResult.PollClosed => Strings.PollClosed(ctx.Guild.Id),
                VoteResult.NotAllowed => Strings.PollVoteNotAllowed(ctx.Guild.Id),
                VoteResult.InvalidOption => Strings.PollInvalidOption(ctx.Guild.Id),
                VoteResult.AlreadyVoted => Strings.PollAlreadyVoted(ctx.Guild.Id),
                _ => Strings.PollVoteError(ctx.Guild.Id)
            };

            if (success)
                await ctx.Interaction.SendEphemeralConfirmAsync(message);
            else
                await ctx.Interaction.SendEphemeralErrorAsync(message, config);

            // Update the poll message with new vote counts
            await UpdatePollMessage(pollIdInt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing poll vote for poll {PollId} by user {UserId}", pollIdInt,
                ctx.User.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollVoteError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    /// Processes select menu interactions for poll voting.
    /// </summary>
    /// <param name="pollId">The ID of the poll being voted on.</param>
    /// <param name="values">The selected option values.</param>
    [ComponentInteraction("poll:select:*")]
    public async Task HandlePollSelect(string pollId, string[] values)
    {
        if (!int.TryParse(pollId, out var pollIdInt))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollInvalid(ctx.Guild.Id), config);
            return;
        }

        var optionIndices = values.Select(v => int.TryParse(v, out var index) ? index : -1)
            .Where(i => i >= 0)
            .ToArray();

        if (optionIndices.Length == 0)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollNoValidOptions(ctx.Guild.Id), config);
            return;
        }

        try
        {
            var (success, result) = await Service.ProcessVoteAsync(pollIdInt, ctx.User.Id, optionIndices);

            var message = result switch
            {
                VoteResult.Success => Strings.PollVoteSuccess(ctx.Guild.Id),
                VoteResult.Changed => Strings.PollVoteChanged(ctx.Guild.Id),
                VoteResult.Removed => Strings.PollVoteRemoved(ctx.Guild.Id),
                VoteResult.PollClosed => Strings.PollClosed(ctx.Guild.Id),
                VoteResult.NotAllowed => Strings.PollVoteNotAllowed(ctx.Guild.Id),
                VoteResult.InvalidOption => Strings.PollInvalidOption(ctx.Guild.Id),
                VoteResult.AlreadyVoted => Strings.PollAlreadyVoted(ctx.Guild.Id),
                _ => Strings.PollVoteError(ctx.Guild.Id)
            };

            if (success)
                await ctx.Interaction.SendEphemeralConfirmAsync(message);
            else
                await ctx.Interaction.SendEphemeralErrorAsync(message, config);

            // Update the poll message with new vote counts
            await UpdatePollMessage(pollIdInt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing poll select for poll {PollId} by user {UserId}", pollIdInt,
                ctx.User.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollVoteError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    /// Processes poll management button interactions.
    /// </summary>
    /// <param name="pollId">The ID of the poll being managed.</param>
    /// <param name="action">The management action to perform.</param>
    [ComponentInteraction("poll:manage:*:*")]
    public async Task HandlePollManage(string pollId, string action)
    {
        var opensModal = action.Equals("delete", StringComparison.OrdinalIgnoreCase);
        if (!opensModal)
            await DeferAsync();
        if (!int.TryParse(pollId, out var pollIdInt))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollInvalid(ctx.Guild.Id), config);
            return;
        }

        try
        {
            var poll = await Service.GetPollAsync(pollIdInt);
            if (poll == null)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollNotFound(ctx.Guild.Id), config);
                return;
            }

            // Check if user has permission to manage the poll
            if (poll.CreatorId != ctx.User.Id && !((IGuildUser)ctx.User).GuildPermissions.ManageMessages)
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollManageNoPermission(ctx.Guild.Id), config);
                return;
            }

            switch (action.ToLower())
            {
                case "close":
                    var closed = await Service.ClosePollAsync(pollIdInt, ctx.User.Id);
                    if (closed)
                    {
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.PollClosedByUser(ctx.Guild.Id));
                        await UpdatePollMessage(pollIdInt);
                    }
                    else
                    {
                        await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.PollCloseFailed(ctx.Guild.Id),
                            config);
                    }

                    break;

                case "delete":
                    await ctx.Interaction.RespondWithModalAsync<DeletePollModal>($"poll:delete:{pollIdInt}");
                    break;

                case "stats":
                    await ShowPollStats(pollIdInt);
                    break;

                default:
                    await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.PollUnknownAction(ctx.Guild.Id),
                        config);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing poll management for poll {PollId} by user {UserId}", pollIdInt,
                ctx.User.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollManageError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    /// Handles poll deletion confirmation modal.
    /// </summary>
    /// <param name="pollId">The ID of the poll to delete.</param>
    /// <param name="modal">The deletion confirmation modal.</param>
    [ModalInteraction("poll:delete:*")]
    public async Task HandleDeletePoll(string pollId, DeletePollModal modal)
    {
        if (!int.TryParse(pollId, out var pollIdInt))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollInvalid(ctx.Guild.Id), config);
            return;
        }

        if (!string.Equals(modal.Confirmation, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollDeleteCancelled(ctx.Guild.Id), config);
            return;
        }

        try
        {
            var deleted = await Service.DeletePollAsync(pollIdInt, ctx.User.Id);
            if (deleted)
            {
                await ctx.Interaction.SendEphemeralConfirmAsync(Strings.PollDeletedSuccessfully(ctx.Guild.Id));

                // Try to delete the poll message
                try
                {
                    var componentInteraction = ctx.Interaction as IComponentInteraction;
                    await componentInteraction.Message.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete poll message for poll {PollId}", pollIdInt);
                }
            }
            else
            {
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollDeleteFailed(ctx.Guild.Id), config);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting poll {PollId} by user {UserId}", pollIdInt, ctx.User.Id);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollDeleteError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    /// Updates the poll message with current vote counts and status.
    /// </summary>
    /// <param name="pollId">The ID of the poll to update.</param>
    private async Task UpdatePollMessage(int pollId)
    {
        try
        {
            var poll = await Service.GetPollAsync(pollId);
            if (poll == null) return;

            var stats = await Service.GetPollStatsAsync(pollId);
            if (stats == null) return;

            // Get the Discord channel and message
            var channel = await ctx.Guild.GetTextChannelAsync(poll.ChannelId);
            if (channel == null) return;

            var message = await channel.GetMessageAsync(poll.MessageId) as IUserMessage;
            if (message == null) return;

            // Rebuild the poll embed with updated vote counts
            var updatedEmbed = await BuildUpdatedPollEmbed(poll, stats);
            var updatedComponents = poll.IsActive
                ? BuildPollComponents(poll)
                : new ComponentBuilder().Build(); // Remove components if closed

            await message.ModifyAsync(msg =>
            {
                msg.Embed = new Optional<Embed>(updatedEmbed);
                msg.Components = new Optional<MessageComponent>(updatedComponents);
            });

            logger.LogDebug("Successfully updated poll message for poll {PollId}", pollId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update poll message for poll {PollId}", pollId);
        }
    }

    /// <summary>
    /// Builds an updated poll embed with current vote statistics.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <param name="stats">The current poll statistics.</param>
    /// <returns>The updated embed.</returns>
    private Task<Embed> BuildUpdatedPollEmbed(Poll poll, PollStats stats)
    {
        var pollType = (PollType)poll.Type;
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
            .WithTitle(Strings.PollTitleFormat(poll.GuildId, typeIcon, poll.Question))
            .WithColor(poll.IsActive ? GetPollColor(pollType) : Color.Red)
            .WithTimestamp(poll.CreatedAt);

        // Add options with vote counts
        for (var i = 0; i < poll.PollOptions.Count(); i++)
        {
            var option = poll.PollOptions.OrderBy(o => o.Index).ElementAt(i);
            var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
            var percentage = stats.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;

            var optionText = $"{option.Index + 1}. {option.Text}";
            if (!string.IsNullOrEmpty(option.Emote))
                optionText = $"{option.Emote} {optionText}";

            var progressBar = BuildProgressBar(percentage);
            var fieldValue = $"{voteCount} votes ({percentage:F1}%)\n{progressBar}";

            embed.AddField(optionText, fieldValue, true);
        }

        // Footer with poll info
        var footerText = pollType switch
        {
            PollType.MultiChoice => Strings.PollFooterMultipleChoice(poll.GuildId),
            PollType.Anonymous => Strings.PollFooterAnonymous(poll.GuildId),
            PollType.RoleRestricted => Strings.PollFooterRoleRestricted(poll.GuildId),
            _ => Strings.PollFooterDefault(poll.GuildId)
        };

        if (!poll.IsActive)
            footerText = poll.ClosedAt.HasValue
                ? Strings.PollStatusClosed(poll.GuildId)
                : Strings.PollStatusExpired(poll.GuildId);

        footerText += $" • Poll ID: {poll.Id} • Total votes: {stats.TotalVotes}";

        embed.WithFooter(footerText);

        return Task.FromResult(embed.Build());
    }

    /// <summary>
    /// Builds a visual progress bar for vote percentages.
    /// </summary>
    /// <param name="percentage">The percentage (0-100).</param>
    /// <returns>A Unicode progress bar string.</returns>
    private static string BuildProgressBar(double percentage)
    {
        const int barLength = 10;
        var filledBlocks = (int)Math.Round(percentage / 100.0 * barLength);
        var emptyBlocks = barLength - filledBlocks;

        return new string('█', filledBlocks) + new string('░', emptyBlocks);
    }

    /// <summary>
    /// Builds the interactive components for the poll.
    /// </summary>
    /// <param name="poll">The poll entity.</param>
    /// <returns>The built message component.</returns>
    private MessageComponent BuildPollComponents(Poll poll)
    {
        var builder = new ComponentBuilder();
        var options = poll.PollOptions.OrderBy(o => o.Index).ToList();
        var pollType = (PollType)poll.Type;

        if (options.Count <= 5)
        {
            // Use buttons for 2-5 options
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var label = $"{option.Index + 1}";
                if (option.Text.Length <= 12)
                    label = $"{option.Index + 1}. {option.Text}";

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
                        emote = new Emoji(option.Emote);
                    }
                }

                var style = GetButtonStyle(option.Index);
                builder.WithButton(label, $"poll:vote:{poll.Id}:{option.Index}", style, emote);
            }
        }
        else
        {
            // Use select menu for 6+ options
            var selectMenuBuilder = new SelectMenuBuilder()
                .WithCustomId($"poll:select:{poll.Id}")
                .WithPlaceholder(Strings.PollSelectPlaceholder(poll.GuildId))
                .WithMinValues(1)
                .WithMaxValues(pollType == PollType.MultiChoice ? Math.Min(options.Count, 25) : 1);

            for (var i = 0; i < Math.Min(options.Count, 25); i++)
            {
                var option = options[i];
                var label = $"{option.Index + 1}. {option.Text}";
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

                selectMenuBuilder.AddOption(label, option.Index.ToString(), emote: emote);
            }

            builder.WithSelectMenu(selectMenuBuilder);
        }

        // Add management buttons on second row
        builder.WithButton("📊 Stats", $"poll:manage:{poll.Id}:stats", ButtonStyle.Secondary)
            .WithButton("🔒 Close", $"poll:manage:{poll.Id}:close", ButtonStyle.Secondary)
            .WithButton("🗑️ Delete", $"poll:manage:{poll.Id}:delete", ButtonStyle.Danger);

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
    /// Shows detailed statistics for a poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll to show stats for.</param>
    private async Task ShowPollStats(int pollId)
    {
        try
        {
            var stats = await Service.GetPollStatsAsync(pollId);
            if (stats == null)
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.PollStatsError(ctx.Guild.Id), config);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PollStatsTitle(ctx.Guild.Id))
                .WithColor(Mewdeko.OkColor)
                .AddField(Strings.PollStatsTotalVotes(ctx.Guild.Id), stats.TotalVotes.ToString(), true)
                .AddField(Strings.PollStatsUniqueVoters(ctx.Guild.Id), stats.UniqueVoters.ToString(), true)
                .AddField(Strings.PollStatsAverageTime(ctx.Guild.Id), stats.AverageVoteTime.ToString(@"hh\:mm\:ss"),
                    true);

            foreach (var kvp in stats.OptionVotes.OrderBy(x => x.Key))
            {
                embed.AddField($"Option {kvp.Key + 1}", $"{kvp.Value} votes", true);
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show poll stats for poll {PollId}", pollId);
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollStatsRetrieveError(ctx.Guild.Id), config);
        }
    }
}