namespace Mewdeko.Modules.Games.Common.Acrophobia
{
    /// <summary>
    /// Represents a user in the Acrophobia game.
    /// </summary>
    public class AcrophobiaUser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AcrophobiaUser"/> class.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="userName">The name of the user.</param>
        /// <param name="input">The input provided by the user.</param>
        public AcrophobiaUser(ulong userId, string userName, string input)
        {
            UserName = userName;
            UserId = userId;
            Input = input;
        }

        /// <summary>
        /// Gets the name of the user.
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// Gets the ID of the user.
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        /// Gets the input provided by the user.
        /// </summary>
        public string Input { get; }

        /// <summary>
        /// Generates a hash code for the user.
        /// </summary>
        /// <returns>The hash code of the user's ID.</returns>
        public override int GetHashCode() => UserId.GetHashCode();

        /// <summary>
        /// Checks whether the current user is equal to the specified object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object? obj) =>
            obj is AcrophobiaUser x
            && x.UserId == UserId;
    }
}