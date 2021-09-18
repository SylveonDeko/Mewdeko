using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;

namespace Mewdeko.Interactive.Selection
{
    /// <summary>
    ///     Represents the base of selections.
    /// </summary>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    public abstract class BaseSelection<TOption> : IInteractiveElement<TOption>
    {
        protected BaseSelection(Func<TOption, IEmote> emoteConverter, Func<TOption, string> stringConverter,
            IEqualityComparer<TOption> equalityComparer,
            bool allowCancel, Page selectionPage, IReadOnlyCollection<IUser> users,
            IReadOnlyCollection<TOption> options, Page canceledPage,
            Page timeoutPage, Page successPage, DeletionOptions deletion, InputType inputType,
            ActionOnStop actionOnCancellation,
            ActionOnStop actionOnTimeout, ActionOnStop actionOnSuccess)
        {
            if (inputType == InputType.Reactions && emoteConverter == null)
                throw new ArgumentNullException(nameof(emoteConverter),
                    $"{nameof(emoteConverter)} is required when {nameof(inputType)} is Reactions.");

            if (stringConverter == null && (inputType != InputType.Buttons || emoteConverter == null))
                stringConverter = x => x.ToString();

            EmoteConverter = emoteConverter;
            StringConverter = stringConverter;
            EqualityComparer = equalityComparer ?? throw new ArgumentNullException(nameof(equalityComparer));
            SelectionPage = selectionPage ?? throw new ArgumentNullException(nameof(selectionPage));

            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.Count == 0)
                throw new ArgumentException($"{nameof(options)} must contain at least one element.", nameof(options));

            if (options.Distinct(EqualityComparer).Count() != options.Count)
                throw new ArgumentException($"{nameof(options)} must not contain duplicate elements.", nameof(options));

            AllowCancel = allowCancel && options.Count > 1;
            CancelOption = AllowCancel ? options.Last() : default;
            Users = users;
            Options = options;
            CanceledPage = canceledPage;
            TimeoutPage = timeoutPage;
            SuccessPage = successPage;
            Deletion = deletion;
            InputType = inputType;
            ActionOnCancellation = actionOnCancellation;
            ActionOnTimeout = actionOnTimeout;
            ActionOnSuccess = actionOnSuccess;
        }

        /// <summary>
        ///     Gets whether the selection is restricted to <see cref="Users" />.
        /// </summary>
        public bool IsUserRestricted => Users?.Count > 0;

        /// <summary>
        ///     Gets a function that returns an <see cref="IEmote" /> representation of a <typeparamref name="TOption" />.
        /// </summary>
        public Func<TOption, IEmote> EmoteConverter { get; set; }

        /// <summary>
        ///     Gets a function that returns a <see cref="string" /> representation of a <typeparamref name="TOption" />.
        /// </summary>
        public Func<TOption, string> StringConverter { get; set; }

        /// <summary>
        ///     Gets the equality comparer of <typeparamref name="TOption" />s.
        /// </summary>
        public IEqualityComparer<TOption> EqualityComparer { get; set; }

        /// <summary>
        ///     Gets whether this selection allows for cancellation.
        /// </summary>
        public bool AllowCancel { get; }

        /// <summary>
        ///     Gets the option used for cancellation.
        /// </summary>
        /// <remarks>
        ///     This option is ignored if <see cref="AllowCancel" /> is <see langword="false" /> or <see cref="Options" />
        ///     contains only one element.
        /// </remarks>
        public TOption CancelOption { get; }

        /// <summary>
        ///     Gets the <see cref="Page" /> which is sent into the channel.
        /// </summary>
        public Page SelectionPage { get; }

        /// <summary>
        ///     Gets or sets the <see cref="Page" /> which this selection gets modified to after a valid input is received
        ///     (except if <see cref="CancelOption" /> is received).
        /// </summary>
        public Page SuccessPage { get; set; }

        /// <summary>
        ///     Gets or sets the action that will be done after valid input is received (except if <see cref="CancelOption" /> is
        ///     received).
        /// </summary>
        public ActionOnStop ActionOnSuccess { get; set; }

        /// <inheritdoc />
        public IReadOnlyCollection<IUser> Users { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<TOption> Options { get; }

        /// <inheritdoc />
        public Page CanceledPage { get; }

        /// <inheritdoc />
        public Page TimeoutPage { get; }

        /// <inheritdoc />
        public DeletionOptions Deletion { get; }

        /// <inheritdoc />
        public InputType InputType { get; }

        /// <inheritdoc />
        public ActionOnStop ActionOnCancellation { get; }

        /// <inheritdoc />
        public ActionOnStop ActionOnTimeout { get; }

#if DNETLABS
        /// <inheritdoc />
        public virtual MessageComponent BuildComponents(bool disableAll)
        {
            if (InputType != InputType.Buttons && InputType != InputType.SelectMenus)
                throw new InvalidOperationException("InputType must be either Buttons or SelectMenus.");

            var builder = new ComponentBuilder();
            if (InputType == InputType.Buttons)
            {
                foreach (var selection in Options)
                {
                    var emote = EmoteConverter?.Invoke(selection);
                    var label = StringConverter?.Invoke(selection);
                    if (emote == null && label == null)
                        throw new InvalidOperationException("Failed to set a valid emote and label to the button.");

                    builder.WithButton(label, emote?.ToString() ?? label, ButtonStyle.Primary, emote, null, disableAll);
                }
            }
            else
            {
                var options = new List<SelectMenuOptionBuilder>();

                foreach (var selection in Options)
                {
                    var emote = EmoteConverter?.Invoke(selection);
                    var label = StringConverter?.Invoke(selection);
                    if (emote == null && label == null)
                        throw new InvalidOperationException(
                            "Failed to set a valid emote and label to the menu option.");

                    var option = new SelectMenuOptionBuilder()
                        .WithLabel(label)
                        .WithEmote(emote)
                        .WithValue(emote?.ToString() ?? label);

                    options.Add(option);
                }

                builder.WithSelectMenu(null, "foobar", options);
            }

            return builder.Build();
        }
#endif

        /// <summary>
        ///     Initializes a message based on this selection.
        /// </summary>
        /// <remarks>
        ///     By default this method adds the reactions to a message when <see cref="InputType" /> is
        ///     <see cref="InputType.Reactions" />.
        /// </remarks>
        /// <param name="message">The message to initialize.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to cancel this request.</param>
        internal virtual async Task InitializeMessageAsync(IUserMessage message,
            CancellationToken cancellationToken = default)
        {
            if (InputType != InputType.Reactions) return;
            if (EmoteConverter == null)
                throw new InvalidOperationException("Reaction-based selections must have a valid emote converter.");

            foreach (var selection in Options)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var emote = EmoteConverter(selection);

                // Only add missing reactions
                if (!message.Reactions.ContainsKey(emote)) await message.AddReactionAsync(emote).ConfigureAwait(false);
            }
        }
    }
}