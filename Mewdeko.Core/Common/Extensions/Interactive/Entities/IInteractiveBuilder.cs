using System.Collections.Generic;
using Discord;

namespace Mewdeko.Interactive
{
    /// <summary>
    ///     Represents a builder of interactive elements.
    /// </summary>
    /// <typeparam name="TElement">The type of the built element.</typeparam>
    /// <typeparam name="TOption">The type of the options.</typeparam>
    /// <typeparam name="TBuilder">The type of this builder.</typeparam>
    public interface IInteractiveBuilder<out TElement, TOption, out TBuilder>
        where TElement : IInteractiveElement<TOption>
        where TBuilder : IInteractiveBuilder<TElement, TOption, TBuilder>
    {
        /// <summary>
        ///     Gets or sets the users who can interact with the <typeparamref name="TElement" />.
        /// </summary>
        ICollection<IUser> Users { get; set; }

        /// <summary>
        ///     Gets or sets a collection of inputs.
        /// </summary>
        ICollection<TOption> Options { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="Page" /> which the <typeparamref name="TElement" /> gets modified to after
        ///     cancellation.
        /// </summary>
        PageBuilder CanceledPage { get; set; }


        /// <summary>
        ///     Gets or sets the <see cref="Page" /> which the <typeparamref name="TElement" /> gets modified to after a timeout.
        /// </summary>
        PageBuilder TimeoutPage { get; set; }

        /// <summary>
        ///     Gets or sets what type of inputs the <typeparamref name="TElement" /> should delete.
        /// </summary>
        DeletionOptions Deletion { get; set; }

        /// <summary>
        ///     Gets or sets input type, that is, what is used to interact with the <typeparamref name="TElement" />.
        /// </summary>
        InputType InputType { get; set; }

        /// <summary>
        ///     Gets or sets the action that will be done after a cancellation.
        /// </summary>
        ActionOnStop ActionOnCancellation { get; set; }

        /// <summary>
        ///     Gets or sets the action that will be done after a timeout.
        /// </summary>
        ActionOnStop ActionOnTimeout { get; set; }

        /// <summary>
        ///     Builds this interactive builder to an immutable <typeparamref name="TElement" />.
        /// </summary>
        /// <returns>An immutable <typeparamref name="TElement" />.</returns>
        TElement Build();

        /// <summary>
        ///     Sets the users who can interact with the <typeparamref name="TElement" />.
        /// </summary>
        /// <param name="users">The users.</param>
        /// <returns>This builder.</returns>
        TBuilder WithUsers(params IUser[] users);

        /// <summary>
        ///     Sets the users who can interact with the <typeparamref name="TElement" />.
        /// </summary>
        /// <param name="users">The users.</param>
        /// <returns>This builder.</returns>
        TBuilder WithUsers(IEnumerable<IUser> users);

        /// <summary>
        ///     Adds a user who can interact with the <typeparamref name="TElement" />.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns>This builder.</returns>
        TBuilder AddUser(IUser user);

        /// <summary>
        ///     Sets the options.
        /// </summary>
        /// <param name="options">A collection of options.</param>
        /// <returns>This builder.</returns>
        TBuilder WithOptions(ICollection<TOption> options);

        /// <summary>
        ///     Adds an option.
        /// </summary>
        /// <param name="option">The option.</param>
        /// <returns>This builder.</returns>
        TBuilder AddOption(TOption option);

        /// <summary>
        ///     Sets the <see cref="Page" /> which the <typeparamref name="TElement" /> gets modified to after a cancellation.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>This builder.</returns>
        TBuilder WithCanceledPage(PageBuilder page);

        /// <summary>
        ///     Sets the <see cref="Page" /> which the <typeparamref name="TElement" /> gets modified to after a timeout.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>This builder.</returns>
        TBuilder WithTimeoutPage(PageBuilder page);

        /// <summary>
        ///     Sets what type of inputs the <typeparamref name="TElement" /> should delete.
        /// </summary>
        /// <param name="deletion">The deletion options.</param>
        /// <returns>This builder.</returns>
        TBuilder WithDeletion(DeletionOptions deletion);

        /// <summary>
        ///     Sets input type, that is, what is used to interact with the <typeparamref name="TElement" />.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>This builder.</returns>
        TBuilder WithInputType(InputType type);

        /// <summary>
        ///     Sets the action that will be done after a cancellation.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>This builder.</returns>
        TBuilder WithActionOnCancellation(ActionOnStop action);

        /// <summary>
        ///     Sets the action that will be done after a timeout.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <returns>This builder.</returns>
        TBuilder WithActionOnTimeout(ActionOnStop action);
    }
}