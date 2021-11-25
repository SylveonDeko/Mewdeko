using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.Extensions.Interactive.Entities;

namespace Mewdeko.Common.Extensions.Interactive.Selection
{
    /// <summary>
    ///     Represents an event handler for a selection.
    /// </summary>
    /// <typeparam name="TOption">The type of the options of the selection.</typeparam>
    internal class SelectionCallback<TOption> : IInteractiveCallback
    {
        private bool _disposed;

        public SelectionCallback(BaseSelection<TOption> selection, IUserMessage message,
            TimeoutTaskCompletionSource<(TOption, InteractiveStatus)> timeoutTaskSource, DateTimeOffset startTime)
        {
            Selection = selection;
            Message = message;
            TimeoutTaskSource = timeoutTaskSource;
            StartTime = startTime;
        }

        /// <summary>
        ///     Gets the selection.
        /// </summary>
        public BaseSelection<TOption> Selection { get; }

        /// <summary>
        ///     Gets the message that contains the selection.
        /// </summary>
        public IUserMessage Message { get; }

        /// <summary>
        ///     Gets the <see cref="TimeoutTaskCompletionSource{TResult}" /> used to set the result of the selection.
        /// </summary>
        public TimeoutTaskCompletionSource<(TOption, InteractiveStatus)> TimeoutTaskSource { get; }

        /// <inheritdoc />
        public DateTimeOffset StartTime { get; }

        /// <inheritdoc />
        public void Cancel()
        {
            TimeoutTaskSource.TryCancel();
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(SocketMessage message)
        {
            if (Selection.InputType != InputType.Messages || !Selection.CanInteract(message.Author)) return;

            var manageMessages = message.Channel is SocketGuildChannel guildChannel
                                 && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            TOption selected = default;
            string selectedString = null;
            foreach (var value in Selection.Options)
            {
                var temp = Selection.StringConverter(value);
                if (temp != message.Content) continue;
                selectedString = temp;
                selected = value;
                break;
            }

            if (selectedString == null)
            {
                if (manageMessages && Selection.Deletion.HasFlag(DeletionOptions.Invalid))
                    await message.DeleteAsync().ConfigureAwait(false);
                return;
            }

            var isCanceled = Selection.AllowCancel &&
                             Selection.StringConverter(Selection.CancelOption) == selectedString;

            if (isCanceled)
            {
                TimeoutTaskSource.TrySetResult((selected, InteractiveStatus.Canceled));
                return;
            }

            if (manageMessages && Selection.Deletion.HasFlag(DeletionOptions.Valid))
                await message.DeleteAsync().ConfigureAwait(false);

            TimeoutTaskSource.TrySetResult((selected, InteractiveStatus.Success));
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(SocketReaction reaction)
        {
            if (Selection.InputType != InputType.Reactions || !Selection.CanInteract(reaction.UserId)) return;

            var manageMessages = Message.Channel is SocketGuildChannel guildChannel
                                 && guildChannel.Guild.CurrentUser.GetPermissions(guildChannel).ManageMessages;

            TOption selected = default;
            IEmote selectedEmote = null;
            foreach (var value in Selection.Options)
            {
                var temp = Selection.EmoteConverter(value);
                if (temp.Name != reaction.Emote.Name) continue;
                selectedEmote = temp;
                selected = value;
                break;
            }

            if (selectedEmote is null)
            {
                if (manageMessages && Selection.Deletion.HasFlag(DeletionOptions.Invalid))
                    await Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);
                return;
            }

            var isCanceled = Selection.AllowCancel &&
                             Selection.EmoteConverter(Selection.CancelOption).Name == selectedEmote.Name;

            if (isCanceled)
            {
                TimeoutTaskSource.TrySetResult((selected, InteractiveStatus.Canceled));
                return;
            }

            if (manageMessages && Selection.Deletion.HasFlag(DeletionOptions.Valid))
                await Message.RemoveReactionAsync(reaction.Emote, reaction.UserId).ConfigureAwait(false);

            TimeoutTaskSource.TrySetResult((selected, InteractiveStatus.Success));
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
            if ((Selection.InputType == InputType.Buttons || Selection.InputType == InputType.SelectMenus)
                && interaction is SocketMessageComponent componentInteraction)
                return ExecuteAsync(componentInteraction);

            return Task.CompletedTask;
        }

        public async Task ExecuteAsync(SocketMessageComponent interaction)
        {
            if (interaction.Message.Id != Message.Id || !Selection.CanInteract(interaction.User)) return;

            TOption selected = default;
            string selectedString = null;
            var customId = Selection.InputType switch
            {
                InputType.Buttons => interaction.Data.CustomId,
                InputType.SelectMenus => (interaction
                        .Message
                        .Components
                        .FirstOrDefault()?
                        .Components
                        .FirstOrDefault() as SelectMenuComponent)?
                    .Options
                    .FirstOrDefault(x => x.Value == interaction.Data.Values.FirstOrDefault())?
                    .Value,
                _ => null
            };

            if (customId == null) return;

            foreach (var value in Selection.Options)
            {
                var stringValue = Selection.EmoteConverter?.Invoke(value)?.ToString() ??
                                  Selection.StringConverter?.Invoke(value);
                if (customId != stringValue) continue;
                selected = value;
                selectedString = stringValue;
                break;
            }

            if (selectedString == null) return;

            await interaction.DeferAsync().ConfigureAwait(false);

            var isCanceled = Selection.AllowCancel
                             && (Selection.EmoteConverter?.Invoke(Selection.CancelOption)?.ToString()
                                 ?? Selection.StringConverter?.Invoke(Selection.CancelOption)) == selectedString;

            TimeoutTaskSource.TrySetResult((selected,
                isCanceled ? InteractiveStatus.Canceled : InteractiveStatus.Success));
            Dispose();
        }
#endif
    }
}