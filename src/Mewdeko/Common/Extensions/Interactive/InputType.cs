namespace Mewdeko.Common.Extensions.Interactive
{
    /// <summary>
    ///     Specifies the types of inputs that are used to interact with the interactive elements.
    /// </summary>
    public enum InputType
    {
        /// <summary>
        ///     Use reactions as input.
        /// </summary>
        Reactions,

        /// <summary>
        ///     Use messages as input.
        /// </summary>
        Messages,

        /// <summary>
        ///     Use buttons as input. Only valid when using Discord.Net Labs.
        /// </summary>
        Buttons,

        /// <summary>
        ///     Use select menus as input. Only valid when using Discord.Net Labs.
        /// </summary>
        SelectMenus
    }
}