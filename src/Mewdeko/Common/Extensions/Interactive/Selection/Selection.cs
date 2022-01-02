using System.Collections.Generic;
using Discord;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;

namespace Mewdeko.Common.Extensions.Interactive.Selection;

/// <summary>
///     Represents a selection of values.
/// </summary>
/// <typeparam name="TOption">The type of the options.</typeparam>
public class Selection<TOption> : BaseSelection<TOption>
{
    internal Selection(Func<TOption, IEmote> emoteConverter, Func<TOption, string> stringConverter,
        IEqualityComparer<TOption> equalityComparer, bool allowCancel, Page selectionPage,
        IReadOnlyCollection<IUser> users, IReadOnlyCollection<TOption> options, Page canceledPage,
        Page timeoutPage, Page successPage, DeletionOptions deletion, InputType inputType,
        ActionOnStop actionOnCancellation, ActionOnStop actionOnTimeout, ActionOnStop actionOnSuccess)
        : base(emoteConverter, stringConverter, equalityComparer, allowCancel, selectionPage, users, options,
            canceledPage, timeoutPage, successPage, deletion, inputType, actionOnCancellation, actionOnTimeout,
            actionOnSuccess)
    {
    }
}