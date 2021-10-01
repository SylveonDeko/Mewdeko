using System.Linq;

namespace Mewdeko.Common.Extensions.Interactive.Selection
{
    /// <summary>
    ///     Represents the default selection builder.
    /// </summary>
    /// <typeparam name="TOption">The type of the options the selection will have.</typeparam>
    public class
        SelectionBuilder<TOption> : BaseSelectionBuilder<Selection<TOption>, TOption, SelectionBuilder<TOption>>
    {
        /// <summary>
        ///     Builds this builder into an immutable selection.
        /// </summary>
        /// <returns>A <see cref="Selection{TOption}" />.</returns>
        public override Selection<TOption> Build()
        {
            return new(EmoteConverter, StringConverter,
                EqualityComparer, AllowCancel, SelectionPage?.Build(), Users?.ToArray(), Options?.ToArray(),
                CanceledPage?.Build(), TimeoutPage?.Build(), SuccessPage?.Build(), Deletion, InputType,
                ActionOnCancellation, ActionOnTimeout, ActionOnSuccess);
        }
    }
}