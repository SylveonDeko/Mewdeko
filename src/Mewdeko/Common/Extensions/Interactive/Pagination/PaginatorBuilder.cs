using System.Collections.Generic;
using System.Linq;
using Discord;
using Mewdeko.Common.Extensions.Interactive.Entities;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;

namespace Mewdeko.Common.Extensions.Interactive.Pagination;

/// <summary>
///     Represents an abstract paginator builder.
/// </summary>
public abstract class PaginatorBuilder<TPaginator, TBuilder>
    : IInteractiveBuilder<TPaginator, KeyValuePair<IEmote, PaginatorAction>, TBuilder>
    where TPaginator : Paginator
    where TBuilder : PaginatorBuilder<TPaginator, TBuilder>
{
    /// <summary>
    ///     Gets whether the paginator is restricted to <see cref="Users" />.
    /// </summary>
    public virtual bool IsUserRestricted => Users?.Count > 0;

    /// <summary>
    ///     Gets or sets the footer format in the <see cref="Embed" /> of the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <remarks>Setting this to other than <see cref="PaginatorFooter.None" /> will override any other footer in the pages.</remarks>
    public virtual PaginatorFooter Footer { get; set; } = PaginatorFooter.PageNumber;

    /// <summary>
    ///     Gets or sets the users who can interact with the <typeparamref name="TPaginator" />.
    /// </summary>
    public virtual IList<IUser> Users { get; set; } = new List<IUser>();

    /// <summary>
    ///     Gets or sets the emotes and their related actions of the <typeparamref name="TPaginator" />.
    /// </summary>
    public virtual IDictionary<IEmote, PaginatorAction> Options { get; set; } =
        new Dictionary<IEmote, PaginatorAction>();

    /// <inheritdoc />
    public virtual PageBuilder CanceledPage { get; set; }

    /// <inheritdoc />
    public virtual PageBuilder TimeoutPage { get; set; }

    /// <inheritdoc />
    /// <remarks>This property is ignored in button-based paginators.</remarks>
    public virtual DeletionOptions Deletion { get; set; } = DeletionOptions.Valid | DeletionOptions.Invalid;

    /// <inheritdoc />
    public virtual InputType InputType { get; set; }
#if DNETLABS
        = InputType.Buttons;
#else
            = InputType.Reactions;
#endif

    /// <inheritdoc />
    /// <remarks>The default value is <see cref="ActionOnStop.ModifyMessage" />.</remarks>
    public virtual ActionOnStop ActionOnCancellation { get; set; } = ActionOnStop.ModifyMessage;

    /// <inheritdoc />
    /// <remarks>The default value is <see cref="ActionOnStop.ModifyMessage" />.</remarks>
    public virtual ActionOnStop ActionOnTimeout { get; set; } = ActionOnStop.ModifyMessage;

    /// <inheritdoc />
    ICollection<KeyValuePair<IEmote, PaginatorAction>>
        IInteractiveBuilder<TPaginator, KeyValuePair<IEmote, PaginatorAction>, TBuilder>.Options
    {
        get => Options;
        set => Options = new Dictionary<IEmote, PaginatorAction>(value);
    }

    ICollection<IUser> IInteractiveBuilder<TPaginator, KeyValuePair<IEmote, PaginatorAction>, TBuilder>.Users
    {
        get => Users;
        set => Users = value?.ToList();
    }

    /// <summary>
    ///     Builds the <see cref="PaginatorBuilder{TPaginator, TBuilder}" /> to an immutable paginator.
    /// </summary>
    /// <param name="startPageIndex">The index of the page the paginator should start.</param>
    /// <returns>A <typeparamref name="TPaginator" />.</returns>
    public abstract TPaginator Build(int startPageIndex = 0);

    /// <summary>
    ///     Gets the footer format in the <see cref="Embed" /> of the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <remarks>Setting this to other than <see cref="PaginatorFooter.None" /> will override any other footer in the pages.</remarks>
    public virtual TBuilder WithFooter(PaginatorFooter footer)
    {
        Footer = footer;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets the users who can interact with the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <param name="users">The users.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithUsers(params IUser[] users)
    {
        Users = users?.ToList();
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets the users who can interact with the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <param name="users">The users.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithUsers(IEnumerable<IUser> users)
    {
        Users = users?.ToList();
        return (TBuilder) this;
    }

    /// <summary>
    ///     Adds a user who can interact with the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder AddUser(IUser user)
    {
        Users?.Add(user);
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets the emotes and their related paginator actions.
    /// </summary>
    /// <param name="emotes">A dictionary of emotes and paginator actions.</param>
    public virtual TBuilder WithOptions(IDictionary<IEmote, PaginatorAction> emotes)
    {
        Options = emotes;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Adds an emote related to a paginator action.
    /// </summary>
    /// <param name="pair">The pair of emote and action.</param>
    public virtual TBuilder AddOption(KeyValuePair<IEmote, PaginatorAction> pair) => AddOption(pair.Key, pair.Value);

    /// <summary>
    ///     Adds an emote related to a paginator action.
    /// </summary>
    /// <param name="emote">The emote.</param>
    /// <param name="action">The paginator action.</param>
    public virtual TBuilder AddOption(IEmote emote, PaginatorAction action)
    {
        Options.Add(emote, action);
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets the <see cref="Page" /> which the <typeparamref name="TPaginator" /> gets modified to after a cancellation.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithCanceledPage(PageBuilder page)
    {
        CanceledPage = page;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets the <see cref="Page" /> which the <typeparamref name="TPaginator" /> gets modified to after a timeout.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithTimeoutPage(PageBuilder page)
    {
        TimeoutPage = page;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets what type of inputs the <typeparamref name="TPaginator" /> should delete.
    /// </summary>
    /// <param name="deletion">The deletion options.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithDeletion(DeletionOptions deletion)
    {
        Deletion = deletion;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Sets input type, that is, what is used to interact with the <typeparamref name="TPaginator" />.
    /// </summary>
    /// <param name="type">The input type.</param>
    /// <returns>This builder.</returns>
    public virtual TBuilder WithInputType(InputType type)
    {
        InputType = type;
        return (TBuilder) this;
    }

    /// <inheritdoc />
    public virtual TBuilder WithActionOnCancellation(ActionOnStop action)
    {
        ActionOnCancellation = action;
        return (TBuilder) this;
    }

    /// <inheritdoc />
    public virtual TBuilder WithActionOnTimeout(ActionOnStop action)
    {
        ActionOnTimeout = action;
        return (TBuilder) this;
    }

    /// <summary>
    ///     Clears all existing emote-action pairs and adds the default emote-action pairs of the
    ///     <typeparamref name="TPaginator" />.
    /// </summary>
    public virtual TBuilder WithDefaultEmotes()
    {
        Options.Clear();

        Options.Add(new Emoji("◀"), PaginatorAction.Backward);
        Options.Add(new Emoji("▶"), PaginatorAction.Forward);
        Options.Add(new Emoji("⏮"), PaginatorAction.SkipToStart);
        Options.Add(new Emoji("⏭"), PaginatorAction.SkipToEnd);
        Options.Add(new Emoji("🛑"), PaginatorAction.Exit);

        return this as TBuilder;
    }

    /// <summary>
    ///     Sets the default canceled page.
    /// </summary>
    public virtual TBuilder WithDefaultCanceledPage() => WithCanceledPage(new PageBuilder().WithColor(Color.Orange).WithTitle("Canceled! 👍"));

    /// <summary>
    ///     Sets the default timeout page.
    /// </summary>
    public virtual TBuilder WithDefaultTimeoutPage() => WithTimeoutPage(new PageBuilder().WithColor(Color.Red).WithTitle("Timed out! ⏰"));

    /// <inheritdoc />
    TPaginator IInteractiveBuilder<TPaginator, KeyValuePair<IEmote, PaginatorAction>, TBuilder>.Build() => Build();

    /// <inheritdoc />
    TBuilder IInteractiveBuilder<TPaginator, KeyValuePair<IEmote, PaginatorAction>, TBuilder>.WithOptions(
        ICollection<KeyValuePair<IEmote, PaginatorAction>> options) =>
        WithOptions(new Dictionary<IEmote, PaginatorAction>(options));
}