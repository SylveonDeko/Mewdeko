using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.Extensions.Interactive.Entities;

namespace Mewdeko.Common.Extensions.Interactive.Pagination;

/// <summary>
///     Represents an event handler for a paginator.
/// </summary>
internal class PaginatorCallback : IInteractiveCallback
{
    private bool _disposed;

    public PaginatorCallback(Paginator paginator, IUserMessage message,
        TimeoutTaskCompletionSource<InteractiveStatus> timeoutTaskSource, DateTimeOffset startTime)
    {
        Paginator = paginator;
        Message = message;
        TimeoutTaskSource = timeoutTaskSource;
        StartTime = startTime;
    }

    /// <summary>
    ///     Gets the paginator.
    /// </summary>
    public Paginator Paginator { get; }

    /// <summary>
    ///     Gets the message that contains the paginator.
    /// </summary>
    public IUserMessage Message { get; }

    /// <summary>
    ///     Gets the <see cref="TimeoutTaskCompletionSource{TResult}" /> used to set the result of the paginator.
    /// </summary>
    public TimeoutTaskCompletionSource<InteractiveStatus> TimeoutTaskSource { get; }

    /// <inheritdoc />
    public DateTimeOffset StartTime { get; }

    /// <inheritdoc />
    public void Cancel()
    {
        TimeoutTaskSource.TryCancel();
    }

    public Task ExecuteAsync(SocketMessage message)
    {
        throw new NotSupportedException("Cannot execute this callback using a message.");
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(SocketReaction reaction)
    {
        if (Paginator.InputType != InputType.Reactions || reaction.MessageId != Message.Id) return;

        var valid = Paginator.Emotes.TryGetValue(reaction.Emote, out var action)
                    && Paginator.CanInteract(reaction.UserId);

        var manageMessages = Message.Channel is SocketGuildChannel guildChannel
                             && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

        if (manageMessages)
            switch (valid)
            {
                case false when Paginator.Deletion.HasFlag(DeletionOptions.Invalid):
                case true when Paginator.Deletion.HasFlag(DeletionOptions.Valid):
                    await Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);
                    break;
            }

        if (!valid) return;

        if (action == PaginatorAction.Exit)
        {
            Cancel();
            return;
        }

        TimeoutTaskSource.TryReset();
        var refreshPage = await Paginator.ApplyActionAsync(action).ConfigureAwait(false);
        if (refreshPage)
        {
            var currentPage = await Paginator.GetOrLoadCurrentPageAsync().ConfigureAwait(false);
            await Message.ModifyAsync(x =>
                {
                    x.Embed = currentPage.Embed;
                    x.Content = currentPage.Text;
                })
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) TimeoutTaskSource.TryDispose();

        _disposed = true;
    }

#if DNETLABS
    /// <inheritdoc />
    public Task ExecuteAsync(SocketInteraction interaction)
    {
        if (Paginator.InputType == InputType.Buttons && interaction is SocketMessageComponent componentInteraction)
            return ExecuteAsync(componentInteraction);

        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(SocketMessageComponent interaction)
    {
        if (interaction.Message.Id != Message.Id) return;
        if (!Paginator.CanInteract(interaction.User))
        {
            await interaction.RespondAsync("You cant use this!", ephemeral: true);
            return;
        }

        var emote = ((ButtonComponent) interaction
                .Message
                .Components
                .FirstOrDefault()?
                .Components?
                .FirstOrDefault(x => x is ButtonComponent button && button.CustomId == interaction.Data.CustomId))?
            .Emote;

        if (emote is null || !Paginator.Emotes.TryGetValue(emote, out var action)) return;

        if (action == PaginatorAction.Exit)
        {
            await interaction.DeferAsync().ConfigureAwait(false);
            Cancel();
            await interaction.Message.DeleteAsync();
            return;
        }

        TimeoutTaskSource.TryReset();
        var refreshPage = await Paginator.ApplyActionAsync(action).ConfigureAwait(false);
        if (refreshPage)
        {
            var currentPage = await Paginator.GetOrLoadCurrentPageAsync().ConfigureAwait(false);
            var buttons = Paginator.BuildComponents(false);
            try
            {
                await interaction.UpdateAsync(x =>
                {
                    x.Content = currentPage.Text;
                    x.Embed = currentPage.Embed;
                    x.Components = buttons;
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }
    }
#endif
}