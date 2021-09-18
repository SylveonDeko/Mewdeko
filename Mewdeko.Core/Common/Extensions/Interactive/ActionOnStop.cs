using System;
using Discord;
using Mewdeko.Interactive.Pagination;

namespace Mewdeko.Interactive
{
    /// <summary>
    ///     Specifies the actions that will be applied to a message after a timeout or a cancellation.
    /// </summary>
    [Flags]
    public enum ActionOnStop
    {
        /// <summary>
        ///     Do nothing.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Modify the message using <see cref="Paginator.TimeoutPage" /> or <see cref="Paginator.CanceledPage" />.
        /// </summary>
        /// <remarks>This action is mutually exclusive with <see cref="DeleteMessage" />.</remarks>
        ModifyMessage = 1 << 0,

        /// <summary>
        ///     Delete the reactions/buttons/select menu from the message.
        /// </summary>
        /// <remarks>
        ///     This action is mutually exclusive with <see cref="DisableInput" />.<br />
        ///     If reactions are used as input, this requires the <see cref="ChannelPermission.ManageMessages" /> permission.
        /// </remarks>
        DeleteInput = 1 << 1,

        /// <summary>
        ///     Disable the buttons or the selection menu from the message. Only applicable to messages using buttons or select
        ///     menus.
        /// </summary>
        /// <remarks>This action is mutually exclusive with <see cref="DeleteInput" />.</remarks>
        DisableInput = 1 << 2,

        /// <summary>
        ///     Delete the message.
        /// </summary>
        /// <remarks>This action takes the highest precedence over any other flag.</remarks>
        DeleteMessage = 1 << 3
    }
}