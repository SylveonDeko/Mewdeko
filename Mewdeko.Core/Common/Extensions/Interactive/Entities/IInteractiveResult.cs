using System;

namespace Mewdeko.Interactive
{
    /// <summary>
    /// Represents a result from an interactive action.
    /// </summary>
    public interface IInteractiveResult
    {
        /// <summary>
        /// Gets the time passed between starting the interactive action and getting its result.
        /// </summary>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// Gets the status of this result.
        /// </summary>
        public InteractiveStatus Status { get; }
    }
}