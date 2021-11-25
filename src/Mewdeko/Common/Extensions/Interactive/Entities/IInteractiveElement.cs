using System.Collections.Generic;
using Discord;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;

namespace Mewdeko.Common.Extensions.Interactive.Entities
{
    /// <summary>
    ///     Represents an interactive element.
    /// </summary>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    public interface IInteractiveElement<out TOption>
    {
        /// <summary>
        ///     Gets a read-only collection of users who can interact with this element.
        /// </summary>
        IReadOnlyCollection<IUser> Users { get; }

        /// <summary>
        ///     Gets a read-only collection of options.
        /// </summary>
        IReadOnlyCollection<TOption> Options { get; }

        /// <summary>
        ///     Gets the <see cref="Page" /> which this element gets modified to after cancellation.
        /// </summary>
        Page.Page CanceledPage { get; }

        /// <summary>
        ///     Gets the <see cref="Page" /> which this element gets modified to after a timeout.
        /// </summary>
        Page.Page TimeoutPage { get; }

        /// <summary>
        ///     Gets what type of inputs this element should delete.
        /// </summary>
        DeletionOptions Deletion { get; }

        /// <summary>
        ///     Gets the input type, that is, what is used to interact with this element.
        /// </summary>
        InputType InputType { get; }

        /// <summary>
        ///     Gets the action that will be done after a cancellation.
        /// </summary>
        ActionOnStop ActionOnCancellation { get; }

        /// <summary>
        ///     Gets the action that will be done after a timeout.
        /// </summary>
        ActionOnStop ActionOnTimeout { get; }

#if DNETLABS
        /// <summary>
        ///     Builds the components that a message that represents this element would have.
        /// </summary>
        /// <remarks>
        ///     This is only used when <see cref="InputType" /> is <see cref="InputType.Buttons" /> or
        ///     <see cref="InputType.SelectMenus" />.
        /// </remarks>
        /// <param name="disableAll">Whether to disable all the components.</param>
        /// <returns>A message component.</returns>
        MessageComponent BuildComponents(bool disableAll);
#endif
    }
}